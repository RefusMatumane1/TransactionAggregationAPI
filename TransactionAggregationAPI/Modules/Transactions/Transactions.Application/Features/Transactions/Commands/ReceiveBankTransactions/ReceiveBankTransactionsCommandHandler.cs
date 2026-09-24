using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Observability;
using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.Audit;
using Modules.Transactions.Application.Common.Inbox;
using Modules.Transactions.Application.Common.Outbox;
using SharedKernel.Abstractions;
using SharedKernel.Common.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Modules.Transactions.Application.Features.Transactions.Commands.ReceiveBankTransactions
{
    internal sealed class ReceiveBankTransactionsCommandHandler(
        IMessagingDbContext messaging,
        ILogger<ReceiveBankTransactionsCommandHandler> logger)
        : ICommandHandler<ReceiveBankTransactionsCommand, InboxReceipt>
    {
        public async Task<Result<InboxReceipt>> Handle(ReceiveBankTransactionsCommand request, CancellationToken cancellationToken)
        {
            var delivery = request.Delivery ?? InboundDelivery.Unspecified;
            var payloadJson = JsonSerializer.Serialize(
                new InboundTransactionsPayload(request.ExternalAccountId, request.Transactions));
            var payloadHash = HashPayload(payloadJson);
            var keyFromSender = !string.IsNullOrWhiteSpace(request.IdempotencyKey);
            var idempotencyKey = keyFromSender ? request.IdempotencyKey!.Trim() : payloadHash;

            var existing = await FindExistingAsync(request.SourceName, idempotencyKey, cancellationToken);
            if (existing is not null)
                return await AcknowledgeDuplicateAsync(existing, request, delivery, idempotencyKey, payloadHash, cancellationToken);

            var inboxMessage = InboxMessage.Create(request.SourceName, payloadJson, idempotencyKey, delivery.Channel, payloadHash);
            messaging.InboxMessages.Add(inboxMessage);

            // Same SaveChanges as the inbox row: the receipt is audited if and only if it was stored.
            var auditMessage = InboundAudit.Enqueue(messaging,
            [
                BuildDeliveryEvent(AuditEventTypes.InboundReceived, request, delivery, inboxMessage.Id.Value,
                    idempotencyKey, payloadHash, detail: $"{request.Transactions.Count} transaction(s) queued",
                    extra: new() { ["idempotencyKeySource"] = keyFromSender ? "sender" : "payload-hash" })
            ]);

            try
            {
                await messaging.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                // Lost a race with a concurrent delivery of the same key (two replicas each
                // receiving the same webhook retry, or a Kafka rebalance overlapping a slow
                // consumer) — the winner's row is the canonical copy.
                messaging.InboxMessages.Entry(inboxMessage).State = EntityState.Detached;
                if (auditMessage is not null)
                    messaging.OutboxMessages.Entry(auditMessage).State = EntityState.Detached;

                existing = await FindExistingAsync(request.SourceName, idempotencyKey, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"Unique violation on inbox key '{idempotencyKey}' but no existing row was found", ex);

                return await AcknowledgeDuplicateAsync(existing, request, delivery, idempotencyKey, payloadHash, cancellationToken);
            }

            return Result.Success(new InboxReceipt(inboxMessage.Id.Value, IsDuplicate: false));
        }

        private Task<InboxMessage?> FindExistingAsync(string sourceName, string idempotencyKey, CancellationToken cancellationToken) =>
            messaging.InboxMessages.FirstOrDefaultAsync(
                m => m.SourceName == sourceName && m.IdempotencyKey == idempotencyKey, cancellationToken);

        private async Task<Result<InboxReceipt>> AcknowledgeDuplicateAsync(
            InboxMessage existing,
            ReceiveBankTransactionsCommand request,
            InboundDelivery delivery,
            string idempotencyKey,
            string payloadHash,
            CancellationToken cancellationToken)
        {
            // Same key, different content: not a redelivery but a sender bug (or a replayed key
            // carrying new data). Acknowledging it as a duplicate would silently drop that data.
            if (!existing.HasSamePayloadAs(payloadHash))
            {
                logger.LogWarning(
                    "Idempotency key reused with a different payload by {SourceName} (inbox message {InboxMessageId}) — refused",
                    request.SourceName, existing.Id.Value);
                return Result.Failure<InboxReceipt>(InboxErrors.IdempotencyKeyReused(idempotencyKey));
            }

            var previousStatus = existing.Status;
            var requeued = existing.RequeueIfDeadLettered();

            DuplicateMetrics.InboundDuplicates.WithLabels(request.SourceName, DuplicateMetrics.MessageLevel).Inc();
            logger.LogWarning(
                "Duplicate inbound delivery from {SourceName} for account {ExternalAccountId} matched inbox message {InboxMessageId} (status {Status}, requeued {Requeued})",
                request.SourceName, request.ExternalAccountId, existing.Id.Value, previousStatus, requeued);

            var notification = new DuplicateInboundDetectedOutboxPayload(
                Level: DuplicateMetrics.MessageLevel,
                SourceName: request.SourceName,
                ExternalAccountId: request.ExternalAccountId,
                DuplicateExternalIds: request.Transactions.Select(t => t.Id).ToList(),
                InboxMessageId: existing.Id.Value,
                CustomerId: null,
                DetectedAt: DateTime.UtcNow);
            messaging.OutboxMessages.Add(OutboxMessage.Create(
                OutboxMessageTypes.DuplicateInboundDetected, JsonSerializer.Serialize(notification)));

            InboundAudit.Enqueue(messaging,
            [
                BuildDeliveryEvent(
                    requeued ? AuditEventTypes.InboundRequeued : AuditEventTypes.InboundDuplicate,
                    request, delivery, existing.Id.Value, idempotencyKey, payloadHash,
                    detail: requeued
                        ? "Replay of a dead-lettered delivery — requeued for processing"
                        : $"Replay of an existing delivery (status {previousStatus}) — not queued again",
                    extra: new() { ["originalStatus"] = previousStatus.ToString() })
            ]);

            await messaging.SaveChangesAsync(cancellationToken);

            return Result.Success(new InboxReceipt(existing.Id.Value, IsDuplicate: true, Requeued: requeued));
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