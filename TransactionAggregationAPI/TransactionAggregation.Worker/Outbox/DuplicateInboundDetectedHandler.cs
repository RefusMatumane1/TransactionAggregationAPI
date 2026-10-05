using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Outbox;
using Modules.Transactions.Application.Common.Outbox;
using System.Text.Json;
using TransactionAggregation.Worker.Notifications;

namespace TransactionAggregation.Worker.Outbox
{
    internal sealed class DuplicateInboundDetectedHandler(INotificationService notifications) : IOutboxMessageHandler
    {
        public string MessageType => OutboxMessageTypes.DuplicateInboundDetected;

        public int HighestReadableSchemaVersion => 1;

        public Task HandleAsync(OutboxMessage message, OutboxDispatchRun run, CancellationToken cancellationToken)
        {
            var duplicate = JsonSerializer.Deserialize<DuplicateInboundDetectedOutboxPayload>(message.Payload)
                ?? throw new PoisonMessageException($"Outbox message {message.Id.Value} has an empty payload");

            return notifications.SendDuplicateInboundAlertAsync(duplicate, cancellationToken);
        }
    }
}