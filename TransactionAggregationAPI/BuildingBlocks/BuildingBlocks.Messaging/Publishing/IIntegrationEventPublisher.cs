namespace BuildingBlocks.Messaging.Publishing
{
    // At least once: consumers deduplicate on MessageId.
    public interface IIntegrationEventPublisher
    {
        Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken);
    }

    public sealed record IntegrationEventEnvelope(
        Guid MessageId,
        string Type,
        int SchemaVersion,
        string Payload,
        string PartitionKey,
        DateTime OccurredAt,
        string? TraceParent,
        string? CorrelationId);
}