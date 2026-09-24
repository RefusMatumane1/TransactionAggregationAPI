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

namespace Modules.Transactions.Infrastructure.Kafka
{
    public enum KafkaMessageOutcome
    {
        Enqueued,
        Duplicate,

        /// <summary>Can never succeed however often it's retried — belongs on the dead-letter topic.</summary>
        Rejected
    }

    public sealed record KafkaMessageResult(KafkaMessageOutcome Outcome, Guid? InboxMessageId = null, string? Reason = null);

    /// <summary>The parts of a consumed Kafka record the handler needs — decoupled from Confluent's types for testability.</summary>
    public sealed record KafkaInboundRecord(
        string Topic,
        int Partition,
        long Offset,
        string? Key,
        string? Value,
        string? IdempotencyKey,
        DateTime? Timestamp,
        string? Source = null);

    /// <summary>
    /// Translates one Kafka record into the same ReceiveBankTransactionsCommand the REST
    /// webhook sends, so both channels share validation, the inbox write and its
    /// idempotency rules. The record value is a BankTransactionsMessage — the webhook's JSON body shape; an optional
    /// "idempotency-key" header plays the role of the webhook's Idempotency-Key header, and an
    /// optional "source" header names the sending source the way the webhook's API key does.
    /// Transient failures (database unavailable, …) are thrown, not returned — the consumer
    /// retries those without committing the offset.
    /// </summary>
    public sealed class BankTransactionsKafkaMessageHandler(
        ISender sender,
        IAuditTrail auditTrail,
        IWebhookSourceDirectory sourceDirectory,
        IOptions<KafkaOptions> options)
    {
        public const string IdempotencyKeyHeader = "idempotency-key";

        /// <summary>
        /// Must name an active source in the WebhookSources registry (the same list the REST
        /// webhook authenticates against). Kafka has no per-record credential, so this trusts
        /// whoever can produce to the topic — topic ACLs are what stop a producer claiming
        /// another source's name.
        /// </summary>
        public const string SourceHeader = "source";

        public async Task<KafkaMessageResult> HandleAsync(KafkaInboundRecord record, CancellationToken cancellationToken)
        {
            // Resolved first so every later rejection is audited under the right source.
            var (sourceName, sourceError) = await ResolveSourceAsync(record, cancellationToken);
            if (sourceError is not null)
                return await RejectAsync(record, sourceName, null, sourceError, cancellationToken);

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

        /// <summary>
        /// Returns the SourceName to use, plus a rejection reason when the record can't be
        /// accepted. An unverified header value is never returned as the SourceName — the
        /// rejection is audited under the configured default, with the claimed name in metadata.
        /// A failing registry lookup throws, so the record is retried rather than dead-lettered.
        /// </summary>
        private async Task<(string SourceName, string? Error)> ResolveSourceAsync(
            KafkaInboundRecord record, CancellationToken cancellationToken)
        {
            var fallback = options.Value.SourceName;
            var claimed = record.Source?.Trim();

            if (string.IsNullOrEmpty(claimed))
                return options.Value.RequireSourceHeader
                    ? (fallback, $"Missing '{SourceHeader}' header")
                    : (fallback, null);

            return await sourceDirectory.IsActiveAsync(claimed, cancellationToken)
                ? (claimed, null)
                : (fallback, $"Unknown or inactive source '{Truncate(claimed, 200)}'");
        }

        /// <summary>
        /// A rejected record never reaches the inbox, so it's audited here. The event id is
        /// derived from topic/partition/offset: if the audit write succeeds but the DLQ produce
        /// then fails, the consumer retries the record and the same fact is stored once.
        /// A failing audit write throws, so the record is retried rather than dead-lettered unaudited.
        /// </summary>
        private async Task<KafkaMessageResult> RejectAsync(
            KafkaInboundRecord record, string sourceName, string? externalAccountId, string reason, CancellationToken cancellationToken)
        {
            var metadata = Describe(record);
            metadata["deadLetterTopic"] = options.Value.DeadLetterTopic;

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