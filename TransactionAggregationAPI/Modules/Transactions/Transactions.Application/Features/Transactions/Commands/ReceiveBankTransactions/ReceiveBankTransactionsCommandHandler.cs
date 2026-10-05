using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Logging;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Observability;
using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.Audit;
using Modules.Transactions.Application.Common.Inbox;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Outbox;
using SharedKernel.Common.Models;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions
{
    internal sealed class ReceiveBankTransactionsCommandHandler(
        IMessagingDbContext messaging,
        ITransactionsDbContext context,
        ILogger<ReceiveBankTransactionsCommandHandler> logger)
        : ICommandHandler<ReceiveBankTransactionsCommand, InboxReceipt>
    {
        public async Task<Result<InboxReceipt>> Handle(ReceiveBankTransactionsCommand request, CancellationToken cancellationToken)
        {
            if (!InboundBank.Matches(request.SourceName, request.Institution))
                return Result.Failure<InboxReceipt>(InboxErrors.InstitutionNotTheSourcesBank(request.SourceName, request.Institution!));

            var delivery = request.Delivery ?? InboundDelivery.Unspecified;
            var payloadJson = JsonSerializer.Serialize(
                new InboundTransactionsPayload(request.ExternalAccountId, request.Institution, request.Transactions));
            var payloadHash = HashPayload(payloadJson);
            var keyFromSender = !string.IsNullOrWhiteSpace(request.IdempotencyKey);
            var idempotencyKey = keyFromSender ? request.IdempotencyKey!.Trim() : payloadHash;

            var known = await FindKnownAsync(request.SourceName, idempotencyKey, cancellationToken);
            if (known is not null)
                return await AcknowledgeDuplicateAsync(known, request, delivery, idempotencyKey, payloadHash, cancellationToken);

            var inboxMessage = InboxMessage.Create(
                request.SourceName, payloadJson, idempotencyKey, delivery.Channel, payloadHash,
                correlationId: delivery.CorrelationId ?? Activity.Current?.TraceId.ToString(),
                traceParent: Activity.Current?.Id);
            messaging.InboxMessages.Add(inboxMessage);

            var received = BuildDeliveryEvent(AuditEventTypes.InboundReceived, request, delivery, inboxMessage.Id.Value,
                idempotencyKey, payloadHash, detail: $"{request.Transactions.Count} transaction(s) queued",
                extra: new() { ["idempotencyKeySource"] = keyFromSender ? "sender" : "payload-hash" });

            context.StageAudit([received]);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                context.DiscardPendingChanges();
                messaging.InboxMessages.Entry(inboxMessage).State = EntityState.Detached;

                known = await FindKnownAsync(request.SourceName, idempotencyKey, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Unique violation on inbox key '{idempotencyKey}' but no existing row was found", ex);

                return await AcknowledgeDuplicateAsync(known, request, delivery, idempotencyKey, payloadHash, cancellationToken);
            }

            logger.LogInformation(
                "Queued delivery {InboxMessageId} from {SourceName} via {Channel}: {TransactionCount} transaction(s) for account {AccountRef}",
                inboxMessage.Id.Value, request.SourceName, delivery.Channel, request.Transactions.Count,
                LogRedaction.Account(request.ExternalAccountId));

            return Result.Success(new InboxReceipt(inboxMessage.Id.Value, IsDuplicate: false));
        }

        private async Task<KnownDelivery?> FindKnownAsync(string sourceName, string idempotencyKey, CancellationToken cancellationToken)
        {
            var live = await messaging.InboxMessages.FirstOrDefaultAsync(
                m => m.SourceName == sourceName && m.IdempotencyKey == idempotencyKey, cancellationToken);
            if (live is not null)
                return new KnownDelivery(live.Id.Value, live.Status, live.HasSamePayloadAs, live.RequeueIfDeadLettered);

            var archived = await messaging.ArchivedInboxMessages.AsNoTracking().FirstOrDefaultAsync(
                m => m.SourceName == sourceName && m.IdempotencyKey == idempotencyKey, cancellationToken);
            return archived is null
                ? null
                : new KnownDelivery(archived.Id, archived.Status, archived.HasSamePayloadAs, static () => false);
        }

        private sealed record KnownDelivery(
            Guid Id, InboxMessageStatus Status, Func<string, bool> HasSamePayloadAs, Func<bool> RequeueIfDeadLettered);

        private async Task<Result<InboxReceipt>> AcknowledgeDuplicateAsync(
            KnownDelivery existing,
            ReceiveBankTransactionsCommand request,
            InboundDelivery delivery,
            string idempotencyKey,
            string payloadHash,
            CancellationToken cancellationToken)
        {
            if (!existing.HasSamePayloadAs(payloadHash))
            {
                logger.LogWarning(
                    "Idempotency key reused with a different payload by {SourceName} (inbox message {InboxMessageId}) — refused",
                    request.SourceName, existing.Id);
                return Result.Failure<InboxReceipt>(InboxErrors.IdempotencyKeyReused(idempotencyKey));
            }

            var previousStatus = existing.Status;
            var requeued = existing.RequeueIfDeadLettered();

            DuplicateMetrics.InboundDuplicates.WithLabels(request.SourceName, DuplicateMetrics.MessageLevel).Inc();
            logger.LogWarning(
                "Duplicate inbound delivery from {SourceName} for account {AccountRef} matched inbox message {InboxMessageId} (status {Status}, requeued {Requeued})",
                request.SourceName, LogRedaction.Account(request.ExternalAccountId), existing.Id, previousStatus, requeued);

            var notification = new DuplicateInboundDetectedOutboxPayload(
                Level: DuplicateMetrics.MessageLevel,
                SourceName: request.SourceName,
                ExternalAccountId: request.ExternalAccountId,
                DuplicateExternalIds: request.Transactions.Select(t => t.Id).ToList(),
                InboxMessageId: existing.Id,
                DetectedAt: DateTime.UtcNow);
            messaging.OutboxMessages.Add(OutboxMessage.Create(
                OutboxMessageTypes.DuplicateInboundDetected, JsonSerializer.Serialize(notification)));

            var replayed = BuildDeliveryEvent(
                requeued ? AuditEventTypes.InboundRequeued : AuditEventTypes.InboundDuplicate,
                request, delivery, existing.Id, idempotencyKey, payloadHash,
                detail: requeued
                    ? "Replay of a dead-lettered delivery — requeued for processing"
                    : $"Replay of an existing delivery (status {previousStatus}) — not queued again",
                extra: new() { ["originalStatus"] = previousStatus.ToString() });

            context.StageAudit([replayed]);
            await context.SaveChangesAsync(cancellationToken);

            return Result.Success(new InboxReceipt(existing.Id, IsDuplicate: true, Requeued: requeued));
        }

        private static AuditEventRecord BuildDeliveryEvent(
            string eventType,
            ReceiveBankTransactionsCommand request,
            InboundDelivery delivery,
            Guid inboxMessageId,
            string idempotencyKey,
            string payloadHash,
            string detail,
            Dictionary<string, string> extra)
        {
            var metadata = new Dictionary<string, string>(delivery.Metadata)
            {
                ["institution"] = request.SourceName,
                ["transactionCount"] = request.Transactions.Count.ToString(),
                ["payloadSha256"] = payloadHash
            };
            foreach (var (key, value) in extra)
                metadata[key] = value;

            return new AuditEventRecord(
                EventId: Guid.NewGuid(),
                EventType: eventType,
                OccurredAt: DateTime.UtcNow,
                Channel: delivery.Channel,
                SourceName: request.SourceName,
                ExternalAccountId: request.ExternalAccountId,
                InboxMessageId: inboxMessageId,
                IdempotencyKey: idempotencyKey,
                Detail: detail,
                Metadata: metadata,
                TraceId: InboundAudit.CurrentTraceId);
        }

        private static string HashPayload(string payloadJson) =>
            "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
    }
}