using BuildingBlocks.Messaging.Outbox;

namespace BuildingBlocks.Messaging.Archiving
{
    public sealed class ArchivedOutboxMessage
    {
        private ArchivedOutboxMessage() { }

        public Guid Id { get; private set; }
        public string Type { get; private set; } = null!;
        public string Payload { get; private set; } = null!;
        public int SchemaVersion { get; private set; }
        public DateTime OccurredAt { get; private set; }
        public string? TraceParent { get; private set; }
        public string? CorrelationId { get; private set; }
        public OutboxMessageStatus Status { get; private set; }
        public int Attempts { get; private set; }
        public DateTime? ClaimedAt { get; private set; }
        public DateTime? NextAttemptAt { get; private set; }
        public DateTime? ProcessedAt { get; private set; }
        public string? LastError { get; private set; }
        public DateTime ArchivedAt { get; private set; }
    }
}