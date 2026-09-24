using SharedKernel.Exceptions;

namespace Modules.Audit.Domain
{
    /// <summary>
    /// An immutable, append-only record of something that happened to inbound data: every
    /// property has a private setter and there are no mutating methods. The database backs
    /// this up with a trigger that rejects UPDATE and DELETE on the table.
    /// </summary>
    public sealed class AuditEvent
    {
        private AuditEvent() { }

        /// <summary>Supplied by the producer (not generated here) — it's the idempotency key.</summary>
        public Guid Id { get; private set; }

        public string EventType { get; private set; } = null!;

        /// <summary>When it happened (producer's clock).</summary>
        public DateTime OccurredAt { get; private set; }

        /// <summary>When the audit row was written — may lag OccurredAt by the outbox poll interval.</summary>
        public DateTime RecordedAt { get; private set; }

        public string Channel { get; private set; } = null!;
        public string SourceName { get; private set; } = null!;
        public string? ExternalAccountId { get; private set; }
        public Guid? InboxMessageId { get; private set; }
        public string? IdempotencyKey { get; private set; }
        public Guid? CustomerId { get; private set; }
        public Guid? TransactionId { get; private set; }
        public string? ExternalTransactionId { get; private set; }
        public string? Detail { get; private set; }
        public Dictionary<string, string> Metadata { get; private set; } = new();
        public string? TraceId { get; private set; }

        public static AuditEvent Create(
            Guid id,
            string eventType,
            DateTime occurredAt,
            string channel,
            string sourceName,
            string? externalAccountId = null,
            Guid? inboxMessageId = null,
            string? idempotencyKey = null,
            Guid? customerId = null,
            Guid? transactionId = null,
            string? externalTransactionId = null,
            string? detail = null,
            IReadOnlyDictionary<string, string>? metadata = null,
            string? traceId = null)
        {
            if (id == Guid.Empty)
                throw new DomainException("Audit event id is required");
            if (string.IsNullOrWhiteSpace(eventType))
                throw new DomainException("Audit event type is required");
            if (string.IsNullOrWhiteSpace(channel))
                throw new DomainException("Audit channel is required");
            if (string.IsNullOrWhiteSpace(sourceName))
                throw new DomainException("Audit source name is required");

            return new AuditEvent
            {
                Id = id,
                EventType = eventType,
                OccurredAt = DateTime.SpecifyKind(occurredAt, DateTimeKind.Utc),
                RecordedAt = DateTime.UtcNow,
                Channel = channel,
                SourceName = sourceName,
                ExternalAccountId = externalAccountId,
                InboxMessageId = inboxMessageId,
                IdempotencyKey = idempotencyKey,
                CustomerId = customerId,
                TransactionId = transactionId,
                ExternalTransactionId = externalTransactionId,
                Detail = Truncate(detail, MaxDetailLength),
                Metadata = metadata is null ? new() : new Dictionary<string, string>(metadata),
                TraceId = traceId
            };
        }

        public const int MaxDetailLength = 2000;

        private static string? Truncate(string? value, int maxLength) =>
            value is null || value.Length <= maxLength ? value : value[..maxLength];
    }
}