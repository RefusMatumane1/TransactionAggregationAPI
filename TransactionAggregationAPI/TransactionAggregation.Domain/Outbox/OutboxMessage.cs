using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Domain.Outbox
{
    public sealed class OutboxMessage
    {
        private OutboxMessage() { }

        public OutboxMessageId Id { get; private set; } = null!;

        public string Type { get; private set; } = null!;

        public string Payload { get; private set; } = null!;

        public DateTime OccurredAt { get; private set; }
        public OutboxMessageStatus Status { get; private set; }
        public int Attempts { get; private set; }
        public DateTime? ClaimedAt { get; private set; }
        public DateTime? NextAttemptAt { get; private set; }
        public DateTime? ProcessedAt { get; private set; }
        public string? LastError { get; private set; }

        public static OutboxMessage Create(string type, string payload) => new()
        {
            Id = OutboxMessageId.Create(),
            Type = type,
            Payload = payload,
            OccurredAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending,
            Attempts = 0
        };

        public void MarkProcessed()
        {
            Status = OutboxMessageStatus.Processed;
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
                Status = OutboxMessageStatus.DeadLettered;
                return;
            }

            Status = OutboxMessageStatus.Pending;
            NextAttemptAt = DateTime.UtcNow.Add(backoff);
        }
    }
}