using BuildingBlocks.Messaging.ValueObjects;

namespace BuildingBlocks.Messaging.Outbox
{
    public sealed class OutboxMessage
    {
        private OutboxMessage() { }

        public OutboxMessageId Id { get; private set; } = null!;

        public string Type { get; private set; } = null!;

        public string Payload { get; private set; } = null!;

        /// <summary>
        /// Version of <see cref="Payload"/>'s schema for this <see cref="Type"/>. A dispatcher
        /// that doesn't know the version dead-letters the message instead of misreading it —
        /// the case during a rolling deploy where a newer producer runs beside an older worker.
        /// </summary>
        public int SchemaVersion { get; private set; } = 1;

        public DateTime OccurredAt { get; private set; }
        public OutboxMessageStatus Status { get; private set; }
        public int Attempts { get; private set; }
        public DateTime? ClaimedAt { get; private set; }
        public DateTime? NextAttemptAt { get; private set; }
        public DateTime? ProcessedAt { get; private set; }
        public string? LastError { get; private set; }

        public static OutboxMessage Create(string type, string payload, int schemaVersion = 1) => new()
        {
            Id = OutboxMessageId.Create(),
            Type = type,
            Payload = payload,
            SchemaVersion = schemaVersion,
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

        /// <summary>Matches the column length: an over-long error must never make the status update itself fail.</summary>
        public const int MaxErrorLength = 2000;

        public void MarkFailed(string error, TimeSpan backoff, int maxAttempts)
        {
            Attempts++;
            LastError = Truncate(error);
            ClaimedAt = null;

            if (Attempts >= maxAttempts)
            {
                Status = OutboxMessageStatus.DeadLettered;
                return;
            }

            Status = OutboxMessageStatus.Pending;
            NextAttemptAt = DateTime.UtcNow.Add(backoff);
        }

        /// <summary>A failure retrying can't fix — dead-lettered now, without spending the retry budget.</summary>
        public void MarkDeadLettered(string error)
        {
            Attempts++;
            LastError = Truncate(error);
            ClaimedAt = null;
            Status = OutboxMessageStatus.DeadLettered;
        }

        private static string Truncate(string error) =>
            error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];
    }
}