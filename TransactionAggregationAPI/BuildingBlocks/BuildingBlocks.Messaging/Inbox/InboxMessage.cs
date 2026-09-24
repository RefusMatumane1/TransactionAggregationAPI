using BuildingBlocks.Messaging.ValueObjects;

namespace BuildingBlocks.Messaging.Inbox
{
    public sealed class InboxMessage
    {
        private InboxMessage() { }

        public InboxMessageId Id { get; private set; } = null!;

        public string SourceName { get; private set; } = null!;

        public string Payload { get; private set; } = null!;

        /// <summary>
        /// Identifies the delivery, not the transactions inside it: unique per SourceName, so
        /// a redelivered webhook call or Kafka record lands on the existing row instead of
        /// queueing a second copy. Null only for rows written before the column existed.
        /// </summary>
        public string? IdempotencyKey { get; private set; }

        /// <summary>
        /// How the delivery arrived (e.g. "webhook", "kafka") — carried here so processing-time
        /// audit events can say which channel the data came through. Null for pre-audit rows.
        /// </summary>
        public string? Channel { get; private set; }

        /// <summary>
        /// SHA-256 of the canonical payload. Lets a redelivery under the same sender-supplied
        /// idempotency key be told apart from a key reused for different content, which must be
        /// refused rather than acknowledged as a duplicate (its data would otherwise be dropped).
        /// Null for rows written before the column existed.
        /// </summary>
        public string? PayloadHash { get; private set; }

        public DateTime ReceivedAt { get; private set; }
        public InboxMessageStatus Status { get; private set; }
        public int Attempts { get; private set; }
        public DateTime? ClaimedAt { get; private set; }
        public DateTime? NextAttemptAt { get; private set; }
        public DateTime? ProcessedAt { get; private set; }
        public string? LastError { get; private set; }

        public static InboxMessage Create(
            string sourceName, string payload, string? idempotencyKey = null, string? channel = null, string? payloadHash = null) => new()
            {
                Id = InboxMessageId.Create(),
                SourceName = sourceName,
                Payload = payload,
                IdempotencyKey = idempotencyKey,
                Channel = channel,
                PayloadHash = payloadHash,
                ReceivedAt = DateTime.UtcNow,
                Status = InboxMessageStatus.Pending,
                Attempts = 0
            };

        /// <summary>
        /// False only when both hashes are known and differ; rows from before the column
        /// existed can't be checked and are treated as matching.
        /// </summary>
        public bool HasSamePayloadAs(string payloadHash) =>
            PayloadHash is null || string.Equals(PayloadHash, payloadHash, StringComparison.Ordinal);

        public void MarkProcessed()
        {
            Status = InboxMessageStatus.Processed;
            ProcessedAt = DateTime.UtcNow;
            ClaimedAt = null;
        }

        /// <summary>
        /// A dead-lettered delivery that arrives again gets another full retry budget rather
        /// than being swallowed as a duplicate — e.g. a bank re-sending a batch after the
        /// BankLink it targets was (re)activated. Returns false for any other status.
        /// </summary>
        public bool RequeueIfDeadLettered()
        {
            if (Status != InboxMessageStatus.DeadLettered)
                return false;

            Status = InboxMessageStatus.Pending;
            Attempts = 0;
            NextAttemptAt = null;
            ClaimedAt = null;
            return true;
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
                Status = InboxMessageStatus.DeadLettered;
                return;
            }

            Status = InboxMessageStatus.Pending;
            NextAttemptAt = DateTime.UtcNow.Add(backoff);
        }

        /// <summary>A failure retrying can't fix — dead-lettered now, without spending the retry budget.</summary>
        public void MarkDeadLettered(string error)
        {
            Attempts++;
            LastError = Truncate(error);
            ClaimedAt = null;
            Status = InboxMessageStatus.DeadLettered;
        }

        private static string Truncate(string error) =>
            error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];
    }
}