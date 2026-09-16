using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Domain.Inbox
{
    /// <summary>
    /// A durable record of a webhook payload as received, before any business processing has
    /// happened. Written and the HTTP response acknowledged in the same request; a background
    /// dispatcher (InboxDispatcherBackgroundService) claims and processes these afterward, with
    /// retry — so the synchronous request path stays cheap regardless of how large a delivery is.
    ///
    /// Not a BaseEntity: it doesn't raise domain events of its own, and CreatedAt/UpdatedAt
    /// aren't meaningful here — ReceivedAt/ProcessedAt/ClaimedAt already cover its lifecycle.
    /// </summary>
    public sealed class InboxMessage
    {
        private InboxMessage() { }

        public InboxMessageId Id { get; private set; }

        /// <summary>Which WebhookSource sent this — carried through for logging/audit, not used
        /// to authorize anything (that already happened before this row was written).</summary>
        public string SourceName { get; private set; }

        /// <summary>JSON of InboundTransactionsPayload — the raw request, not yet validated
        /// against business state (e.g. whether the BankLink it references actually exists).</summary>
        public string Payload { get; private set; }

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

        /// <summary>Records a failed attempt. Schedules a retry after `backoff` unless this was
        /// the last allowed attempt, in which case the message is dead-lettered instead.</summary>
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
