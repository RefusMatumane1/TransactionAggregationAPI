using BuildingBlocks.Messaging.ValueObjects;

namespace BuildingBlocks.Messaging.Inbox
{
    // A delivery waiting to be (or already) processed. Attempts counts claims: a claim is an attempt
    // whether it ends in success, a recorded failure, or a crash that leaves the claim to expire, so a
    // message that keeps killing its worker still runs out of attempts and is dead-lettered.
    public sealed class InboxMessage
    {
        public const int MaxCorrelationIdLength = 64;
        public const int MaxTraceParentLength = 64;
        public const int MaxErrorLength = 2000;

        private InboxMessage() { }

        public InboxMessageId Id { get; private set; } = null!;
        public string SourceName { get; private set; } = null!;
        public string Payload { get; private set; } = null!;
        public string? IdempotencyKey { get; private set; }
        public string? Channel { get; private set; }
        public string? PayloadHash { get; private set; }
        public string? CorrelationId { get; private set; }
        public string? TraceParent { get; private set; }
        public DateTime ReceivedAt { get; private set; }
        public InboxMessageStatus Status { get; private set; }
        public int Attempts { get; private set; }
        public DateTime? ClaimedAt { get; private set; }
        public DateTime? NextAttemptAt { get; private set; }
        public DateTime? ProcessedAt { get; private set; }
        public string? LastError { get; private set; }

        public static InboxMessage Create(
            string sourceName,
            string payload,
            string? idempotencyKey = null,
            string? channel = null,
            string? payloadHash = null,
            string? correlationId = null,
            string? traceParent = null) => new()
            {
                Id = InboxMessageId.Create(),
                SourceName = sourceName,
                Payload = payload,
                IdempotencyKey = idempotencyKey,
                Channel = channel,
                PayloadHash = payloadHash,
                CorrelationId = Bound(correlationId, MaxCorrelationIdLength),
                TraceParent = Bound(traceParent, MaxTraceParentLength),
                ReceivedAt = DateTime.UtcNow,
                Status = InboxMessageStatus.Pending,
                Attempts = 0
            };

        public bool HasSamePayloadAs(string payloadHash) =>
            PayloadHash is null || string.Equals(PayloadHash, payloadHash, StringComparison.Ordinal);

        // The state change MessagingDbContext.ClaimInboxMessagesAsync applies set-wise in SQL.
        public void Claim(DateTime now)
        {
            Status = InboxMessageStatus.Processing;
            ClaimedAt = now;
            Attempts++;
        }

        // Hands back a claim that was never worked on (shutdown), without spending an attempt.
        public void ReleaseClaim()
        {
            if (Status != InboxMessageStatus.Processing)
                return;

            Status = InboxMessageStatus.Pending;
            ClaimedAt = null;
            Attempts = Math.Max(0, Attempts - 1);
        }

        public void MarkProcessed()
        {
            Status = InboxMessageStatus.Processed;
            ProcessedAt = DateTime.UtcNow;
            ClaimedAt = null;
        }

        public void MarkFailed(string error, TimeSpan backoff, int maxAttempts)
        {
            LastError = Truncate(error);
            ClaimedAt = null;
            ProcessedAt = null;

            if (Attempts >= maxAttempts)
            {
                Status = InboxMessageStatus.DeadLettered;
                return;
            }

            Status = InboxMessageStatus.Pending;
            NextAttemptAt = DateTime.UtcNow.Add(backoff);
        }

        public void MarkDeadLettered(string error)
        {
            LastError = Truncate(error);
            ClaimedAt = null;
            ProcessedAt = null;
            Status = InboxMessageStatus.DeadLettered;
        }

        // A sender redelivering a dead-lettered message gets a fresh retry budget.
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

        private static string? Bound(string? value, int maxLength) =>
            value is null || value.Length <= maxLength ? value : null;

        private static string Truncate(string error) =>
            error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];
    }
}