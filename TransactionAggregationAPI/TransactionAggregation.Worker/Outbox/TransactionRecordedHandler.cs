using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Publishing;
using Modules.Transactions.Application.Common.Caching;
using Modules.Transactions.Contracts.IntegrationEvents;
using System.Text.Json;

namespace TransactionAggregation.Worker.Outbox
{
    // Makes a new ledger entry visible: cached reads are invalidated, then the event is published
    // for other systems. Both are idempotent, so a retry after a partial success is safe.
    internal sealed class TransactionRecordedHandler(IIntegrationEventPublisher publisher) : IOutboxMessageHandler
    {
        public string MessageType => TransactionRecorded.EventType;

        public int HighestReadableSchemaVersion => TransactionRecorded.SchemaVersion;

        public async Task HandleAsync(OutboxMessage message, OutboxDispatchRun run, CancellationToken cancellationToken)
        {
            var recorded = JsonSerializer.Deserialize<TransactionRecorded>(message.Payload)
                ?? throw new PoisonMessageException($"Outbox message {message.Id.Value} has an empty payload");

            await run.InvalidateCacheScopeOnceAsync(TransactionCacheScopes.All, cancellationToken);

            await publisher.PublishAsync(new IntegrationEventEnvelope(
                MessageId: message.Id.Value,
                Type: message.Type,
                SchemaVersion: message.SchemaVersion,
                Payload: message.Payload,
                PartitionKey: $"{recorded.Institution}:{recorded.ExternalAccountId}",
                OccurredAt: message.OccurredAt,
                TraceParent: message.TraceParent,
                CorrelationId: message.CorrelationId), cancellationToken);
        }
    }
}