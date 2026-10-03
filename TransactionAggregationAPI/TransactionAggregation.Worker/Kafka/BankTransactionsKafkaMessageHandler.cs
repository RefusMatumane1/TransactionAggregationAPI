using BuildingBlocks.Messaging;
using MediatR;
using Microsoft.Extensions.Options;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.Audit;
using Modules.Transactions.Application.Common.Inbox;
using Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions;
using Modules.WebhookSources.Contracts;
using SharedKernel.Common.Enums;
using System.Globalization;
using System.Text.Json;

namespace TransactionAggregation.Worker.Kafka
{
    public enum KafkaMessageOutcome
    {
        Enqueued,
        Duplicate,

        Rejected
    }

    public sealed record KafkaMessageResult(KafkaMessageOutcome Outcome, Guid? InboxMessageId = null, string? Reason = null);

    public sealed record KafkaInboundRecord(
        string Topic,
        int Partition,
        long Offset,
        string? Key,
        string? Value,
        string? IdempotencyKey,
        DateTime? Timestamp,
        string? Source = null,
        string? Signature = null);

    public sealed class BankTransactionsKafkaMessageHandler(
        ISender sender,
        IAuditTrail auditTrail,
        IWebhookSourceDirectory sourceDirectory,
        IOptions<KafkaOptions> options,
        ILogger<BankTransactionsKafkaMessageHandler> logger)
    {
        public const string IdempotencyKeyHeader = "idempotency-key";

        public const string SourceHeader = "source";

        public const string SignatureHeader = "signature";

        public async Task<KafkaMessageResult> HandleAsync(KafkaInboundRecord record, CancellationToken cancellationToken)
        {
            var (sourceName, sourceError) = await AuthenticateSourceAsync(record, cancellationToken);
            if (sourceError is not null)
                return await RejectAsync(record, AuditSources.Unauthenticated, null, sourceError, cancellationToken);

            if (string.IsNullOrWhiteSpace(record.Value))
                return await RejectAsync(record, sourceName, null, "Empty message value", cancellationToken);

            BankTransactionsMessage? request;
            try
            {
                request = JsonSerializer.Deserialize<BankTransactionsMessage>(record.Value, JsonSerializerOptions.Web);
            }
            catch (JsonException ex)
            {
                return await RejectAsync(record, sourceName, null, $"Malformed JSON: {ex.Message}", cancellationToken);
            }

            if (request?.Transactions is null)
                return await RejectAsync(record, sourceName, request?.ExternalAccountId, "Message has no transactions array", cancellationToken);

            var command = new ReceiveBankTransactionsCommand(
                sourceName,
                request.ExternalAccountId,
                request.Institution,
                request.ToExternalTransactionDtos(),
                string.IsNullOrWhiteSpace(record.IdempotencyKey) ? null : record.IdempotencyKey,
                new InboundDelivery(AuditChannels.Kafka, Describe(record)),
                request.EffectiveSchemaVersion);

            var result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                if (result.Error.Type is ErrorType.Validation || result.Error.Code == InboxErrors.IdempotencyKeyReusedCode)
                    return await RejectAsync(record, sourceName, request.ExternalAccountId, result.Error.Description, cancellationToken);

                throw new InvalidOperationException(
                    $"Receiving Kafka message failed: {result.Error.Code} {result.Error.Description}");
            }

            return new(
                result.Value.IsDuplicate && !result.Value.Requeued ? KafkaMessageOutcome.Duplicate : KafkaMessageOutcome.Enqueued,
                result.Value.InboxMessageId);
        }

        private async Task<(string SourceName, string? Error)> AuthenticateSourceAsync(
            KafkaInboundRecord record, CancellationToken cancellationToken)
        {
            var claimed = record.Source?.Trim();

            if (string.IsNullOrEmpty(claimed))
                return (AuditSources.Unauthenticated, $"Missing '{SourceHeader}' header");

            if (HasControlCharacters(claimed) || HasControlCharacters(record.IdempotencyKey))
                return (AuditSources.Unauthenticated, $"Control characters in the '{SourceHeader}' or '{IdempotencyKeyHeader}' header");

            if (string.IsNullOrWhiteSpace(record.Signature))
                return (AuditSources.Unauthenticated, $"Missing '{SignatureHeader}' header");

            byte[] signature;
            try
            {
                signature = Convert.FromBase64String(record.Signature.Trim());
            }
            catch (FormatException)
            {
                return (AuditSources.Unauthenticated, $"The '{SignatureHeader}' header is not base64");
            }

            var signedContent = KafkaRecordSignature.SignedContent(claimed, record.IdempotencyKey, record.Value);
            var source = Truncate(claimed, 200);

            return await sourceDirectory.VerifySignatureAsync(claimed, signedContent, signature, cancellationToken) switch
            {
                SignatureVerification.Valid => (claimed, null),
                SignatureVerification.UnknownOrInactiveSource => (AuditSources.Unauthenticated, $"Unknown or inactive source '{source}'"),
                SignatureVerification.NoSigningKeyRegistered => (AuditSources.Unauthenticated, $"Source '{source}' has no registered signing key"),
                _ => (AuditSources.Unauthenticated, $"Signature does not verify for source '{source}'")
            };
        }

        private static bool HasControlCharacters(string? value) => value is not null && value.Any(char.IsControl);

        public Task<KafkaMessageResult> DeadLetterAsync(KafkaInboundRecord record, string reason, CancellationToken cancellationToken) =>
            RejectAsync(record, AuditSources.Unauthenticated, externalAccountId: null, reason, cancellationToken);

        private async Task<KafkaMessageResult> RejectAsync(
            KafkaInboundRecord record, string sourceName, string? externalAccountId, string reason, CancellationToken cancellationToken)
        {
            var metadata = Describe(record);
            metadata["deadLetterTopic"] = options.Value.DeadLetterTopic;

            try
            {
                await auditTrail.RecordAsync(
                [
                    new AuditEventRecord(
                        EventId: AuditEventIds.Deterministic($"kafka:{record.Topic}:{record.Partition}:{record.Offset}:rejected"),
                        EventType: AuditEventTypes.InboundRejected,
                        OccurredAt: DateTime.UtcNow,
                        Channel: AuditChannels.Kafka,
                        SourceName: sourceName,
                        ExternalAccountId: string.IsNullOrWhiteSpace(externalAccountId) ? null : externalAccountId,
                        IdempotencyKey: record.IdempotencyKey,
                        Detail: reason,
                        Metadata: metadata,
                        TraceId: InboundAudit.CurrentTraceId)
                ], cancellationToken);
            }
            catch (Exception ex) when (FailureClassifier.Classify(ex) == FailureKind.Permanent)
            {
                logger.LogCritical(ex,
                    "Audit record for rejected Kafka record {Topic}/{Partition}@{Offset} was refused by the database — dead-lettering without it",
                    record.Topic, record.Partition, record.Offset);
            }

            return new(KafkaMessageOutcome.Rejected, Reason: reason);
        }

        private Dictionary<string, string> Describe(KafkaInboundRecord record)
        {
            var metadata = new Dictionary<string, string>
            {
                ["topic"] = record.Topic,
                ["partition"] = record.Partition.ToString(CultureInfo.InvariantCulture),
                ["offset"] = record.Offset.ToString(CultureInfo.InvariantCulture),
                ["consumerGroup"] = options.Value.GroupId,
                ["idempotencyKeyProvided"] = string.IsNullOrWhiteSpace(record.IdempotencyKey) ? "false" : "true"
            };
            if (!string.IsNullOrEmpty(record.Key))
                metadata["key"] = Truncate(record.Key, 200);
            if (!string.IsNullOrWhiteSpace(record.Source))
                metadata["sourceHeader"] = Truncate(record.Source.Trim(), 200);
            if (record.Timestamp is { } timestamp)
                metadata["producedAt"] = timestamp.ToString("O", CultureInfo.InvariantCulture);
            return metadata;
        }

        private static string Truncate(string value, int maxLength) =>
            value.Length <= maxLength ? value : value[..maxLength];
    }
}