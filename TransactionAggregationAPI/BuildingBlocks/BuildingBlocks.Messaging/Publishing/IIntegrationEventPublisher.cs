namespace BuildingBlocks.Messaging.Publishing
{
    // Publishes an outbox message to other systems. Delivery is at least once: the outbox retries a
    // publish whose acknowledgement was lost, so consumers deduplicate on MessageId.
    public interface IIntegrationEventPublisher
    {
        Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken);
    }

    public sealed record IntegrationEventEnvelope(
        Guid MessageId,
        string Type,
        int SchemaVersion,
        string Payload,
        // Events with the same key are delivered in order (one bank account's transactions).
        string PartitionKey,
        DateTime OccurredAt,
        string? TraceParent,
        string? CorrelationId);
}