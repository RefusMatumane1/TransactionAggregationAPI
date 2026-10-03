using BuildingBlocks.Messaging.Inbox;

namespace BuildingBlocks.Messaging.Archiving
{
    public sealed class ArchivedInboxMessage
    {
        private ArchivedInboxMessage() { }

        public Guid Id { get; private set; }
        public string SourceName { get; private set; } = null!;
        public string Payload { get; private set; } = null!;
        public DateTime ReceivedAt { get; private set; }
        public InboxMessageStatus Status { get; private set; }
        public int Attempts { get; private set; }
        public DateTime? ClaimedAt { get; private set; }
        public DateTime? NextAttemptAt { get; private set; }
        public DateTime? ProcessedAt { get; private set; }
        public string? LastError { get; private set; }
        public string? IdempotencyKey { get; private set; }
        public string? Channel { get; private set; }
        public string? PayloadHash { get; private set; }
        public string? CorrelationId { get; private set; }
        public string? TraceParent { get; private set; }
        public DateTime ArchivedAt { get; private set; }

        public bool HasSamePayloadAs(string payloadHash) =>
            PayloadHash is null || string.Equals(PayloadHash, payloadHash, StringComparison.Ordinal);
    }
}