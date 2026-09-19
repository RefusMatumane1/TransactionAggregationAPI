using BuildingBlocks.Messaging.ValueObjects;

namespace BuildingBlocks.Messaging.Inbox
{
    public sealed class InboxMessage
    {
        private InboxMessage() { }

        public InboxMessageId Id { get; private set; } = null!;

        public string SourceName { get; private set; } = null!;

        public string Payload { get; private set; } = null!;

        public DateTime ReceivedAt { get; private set; }
        public InboxMessageStatus Status { get; private set; }
        public int Attempts { get; private set; }
        public DateTime? ClaimedAt { get; private set; }
        public DateTime? NextAttemptAt { get; private set; }
        public DateTime? ProcessedAt { get; private set; }
        public string? LastError { get; private set; }

        public static InboxMessage Create(string sourceName, string payload) => new()
        {
            Id = InboxMessageId.Create(),
            SourceName = sourceName,
            Payload = payload,
            ReceivedAt = DateTime.UtcNow,
            Status = InboxMessageStatus.Pending,
            Attempts = 0
        };

        public void MarkProcessed()
        {
            Status = InboxMessageStatus.Processed;
            ProcessedAt = DateTime.UtcNow;
            ClaimedAt = null;
        }

        public void MarkFailed(string error, TimeSpan backoff, int maxAttempts)
        {
            Attempts++;
            LastError = error;
            ClaimedAt = null;

            if (Attempts >= maxAttempts)
            {
                Status = InboxMessageStatus.DeadLettered;
                return;
            }

            Status = InboxMessageStatus.Pending;
            NextAttemptAt = DateTime.UtcNow.Add(backoff);
        }
    }
}
