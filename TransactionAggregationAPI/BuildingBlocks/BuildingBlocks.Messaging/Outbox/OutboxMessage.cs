using BuildingBlocks.Messaging.ValueObjects;
using System.Diagnostics;

namespace BuildingBlocks.Messaging.Outbox
{
    // Work committed with the business change that caused it. Attempts counts claims, as for the
    // inbox. TraceParent and CorrelationId carry the producing request's trace to the dispatch.
    public sealed class OutboxMessage
    {
        public const int MaxErrorLength = 2000;
        public const int MaxTraceContextLength = 64;

        private OutboxMessage() { }

        public OutboxMessageId Id { get; private set; } = null!;
        public string Type { get; private set; } = null!;
        public string Payload { get; private set; } = null!;
        public int SchemaVersion { get; private set; } = 1;
        public DateTime OccurredAt { get; private set; }
        public string? TraceParent { get; private set; }
        public string? CorrelationId { get; private set; }
        public OutboxMessageStatus Status { get; private set; }
        public int Attempts { get; private set; }
        public DateTime? ClaimedAt { get; private set; }
        public DateTime? NextAttemptAt { get; private set; }
        public DateTime? ProcessedAt { get; private set; }
        public string? LastError { get; private set; }

        public static OutboxMessage Create(string type, string payload, int schemaVersion = 1) =>
            Create(type, _ => payload, schemaVersion);

        // For payloads that embed their own message id (integration events deduplicated by it).
        public static OutboxMessage Create(string type, Func<Guid, string> payloadFor, int schemaVersion = 1)
        {
            var id = OutboxMessageId.Create();
            var activity = Activity.Current;
            return new OutboxMessage
            {
                Id = id,
                Type = type,
                Payload = payloadFor(id.Value),
                SchemaVersion = schemaVersion,
                OccurredAt = DateTime.UtcNow,
                TraceParent = Bound(activity?.Id),
                CorrelationId = Bound(activity?.GetBaggageItem(Observability.MessagingTelemetry.CorrelationBaggageKey) ?? activity?.TraceId.ToString()),
                Status = OutboxMessageStatus.Pending,
                Attempts = 0
            };
        }

        // The state change MessagingDbContext.ClaimOutboxMessagesAsync applies set-wise in SQL.
        public void Claim(DateTime now)
        {
            Status = OutboxMessageStatus.Processing;
            ClaimedAt = now;
            Attempts++;
        }

        public void ReleaseClaim()
        {
            if (Status != OutboxMessageStatus.Processing)
                return;

            Status = OutboxMessageStatus.Pending;
            ClaimedAt = null;
            Attempts = Math.Max(0, Attempts - 1);
        }

        public void MarkProcessed()
        {
            Status = OutboxMessageStatus.Processed;
            ProcessedAt = DateTime.UtcNow;
            ClaimedAt = null;
        }

        public void MarkFailed(string error, TimeSpan backoff, int maxAttempts)
        {
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

        public void MarkDeadLettered(string error)
        {
            LastError = Truncate(error);
            ClaimedAt = null;
            Status = OutboxMessageStatus.DeadLettered;
        }

        private static string? Bound(string? value) =>
            value is null || value.Length <= MaxTraceContextLength ? value : null;

        private static string Truncate(string error) =>
            error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];
    }
}