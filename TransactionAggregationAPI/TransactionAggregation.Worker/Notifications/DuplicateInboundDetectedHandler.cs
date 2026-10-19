using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Outbox;
using Modules.Notifications.Contracts;
using Modules.Transactions.Application.Common.Outbox;
using System.Text.Json;
using TransactionAggregation.Worker.Messaging;

namespace TransactionAggregation.Worker.Notifications
{
    internal sealed class DuplicateInboundDetectedHandler(IDuplicateAlertNotifier notifier) : IOutboxMessageHandler
    {
        public string MessageType => OutboxMessageTypes.DuplicateInboundDetected;

        public int HighestReadableSchemaVersion => 1;

        public Task HandleAsync(OutboxMessage message, OutboxDispatchRun run, CancellationToken cancellationToken)
        {
            var duplicate = JsonSerializer.Deserialize<DuplicateInboundDetectedOutboxPayload>(message.Payload)
                ?? throw new PoisonMessageException($"Outbox message {message.Id.Value} has an empty payload");

            return notifier.NotifyAsync(
                new DuplicateAlert(
                    duplicate.Level,
                    duplicate.SourceName,
                    duplicate.ExternalAccountId,
                    duplicate.DuplicateExternalIds,
                    duplicate.InboxMessageId,
                    duplicate.DetectedAt),
                cancellationToken);
        }
    }
}