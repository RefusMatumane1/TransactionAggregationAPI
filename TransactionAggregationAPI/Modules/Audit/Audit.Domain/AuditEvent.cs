using SharedKernel.Exceptions;

namespace Modules.Audit.Domain
{
    public sealed class AuditEvent
    {
        private AuditEvent() { }

        public Guid Id { get; private set; }

        public string EventType { get; private set; } = null!;

        public DateTime OccurredAt { get; private set; }

        public DateTime RecordedAt { get; private set; }

        public string Channel { get; private set; } = null!;
        public string SourceName { get; private set; } = null!;
        public string? ExternalAccountId { get; private set; }
        public Guid? InboxMessageId { get; private set; }
        public string? IdempotencyKey { get; private set; }
        public Guid? TransactionId { get; private set; }
        public string? ExternalTransactionId { get; private set; }
        public string? Detail { get; private set; }
        public Dictionary<string, string> Metadata { get; private set; } = new();
        public string? TraceId { get; private set; }

        // The signed-in user (identity-provider subject id) behind an administrative change.
        public string? Actor { get; private set; }

        public static AuditEvent Create(
            Guid id,
            string eventType,
            DateTime occurredAt,
            string channel,
            string sourceName,
            string? externalAccountId = null,
            Guid? inboxMessageId = null,
            string? idempotencyKey = null,
            Guid? transactionId = null,
            string? externalTransactionId = null,
            string? detail = null,
            IReadOnlyDictionary<string, string>? metadata = null,
            string? traceId = null,
            string? actor = null)
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
                SourceName = Clean(sourceName, MaxIdentifierLength)!,
                ExternalAccountId = Clean(externalAccountId, MaxIdentifierLength),
                InboxMessageId = inboxMessageId,
                IdempotencyKey = Clean(idempotencyKey, MaxIdentifierLength),
                TransactionId = transactionId,
                ExternalTransactionId = Clean(externalTransactionId, MaxExternalTransactionIdLength),
                Detail = Clean(detail, MaxDetailLength),
                Metadata = metadata is null
                    ? new()
                    : metadata.ToDictionary(kv => Clean(kv.Key, MaxDetailLength)!, kv => Clean(kv.Value, MaxDetailLength)!),
                TraceId = Clean(traceId, MaxTraceIdLength),
                Actor = Clean(actor, MaxActorLength)
            };
        }

        public const int MaxDetailLength = 2000;
        public const int MaxIdentifierLength = 200;
        public const int MaxExternalTransactionIdLength = 100;
        public const int MaxTraceIdLength = 64;
        public const int MaxActorLength = 64;

        private static string? Clean(string? value, int maxLength)
        {
            if (value is null)
                return null;

            if (value.Contains('\0'))
                value = value.Replace("\0", string.Empty);

            return value.Length <= maxLength ? value : value[..maxLength];
        }
    }
}