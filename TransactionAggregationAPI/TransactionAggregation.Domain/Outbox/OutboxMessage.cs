using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Domain.Outbox
{
    /// <summary>
    /// A durable record of a side effect (cache invalidation, analytics, notification) that
    /// still needs to happen, written in the same SaveChangesAsync call — and therefore the same
    /// DB transaction — as the business data it describes. A background dispatcher
    /// (OutboxDispatcherBackgroundService) claims and processes these later, with retry.
    ///
    /// Not a BaseEntity: it doesn't raise domain events of its own, and CreatedAt/UpdatedAt
    /// aren't meaningful here — OccurredAt/ProcessedAt/ClaimedAt already cover its lifecycle.
    /// </summary>
    public sealed class OutboxMessage
    {
        private OutboxMessage() { }

        public OutboxMessageId Id { get; private set; }

        /// <summary>Short discriminator (e.g. "TransactionSynced") the dispatcher uses to pick
        /// which payload type to deserialize Payload as and which handler logic to run.</summary>
        public string Type { get; private set; }

        /// <summary>JSON of a small, purpose-built payload record — never the domain entity
        /// itself (not safely serializable, and shouldn't be re-hydrated as a tracked entity).</summary>
        public string Payload { get; private set; }

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

        /// <summary>Records a failed attempt. Schedules a retry after `backoff` unless this was
        /// the last allowed attempt, in which case the message is dead-lettered instead.</summary>
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
