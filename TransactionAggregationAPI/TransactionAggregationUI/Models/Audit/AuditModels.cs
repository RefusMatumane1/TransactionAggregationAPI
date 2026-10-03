namespace TransactionAggregationUI.Models.Audit
{
    public class AuditEventModel
    {
        public Guid Id { get; set; }
        public string EventType { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
        public DateTime RecordedAt { get; set; }
        public string Channel { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public string? ExternalAccountId { get; set; }
        public Guid? InboxMessageId { get; set; }
        public string? IdempotencyKey { get; set; }
        public Guid? TransactionId { get; set; }
        public string? ExternalTransactionId { get; set; }
        public string? Detail { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public string? TraceId { get; set; }

        // Subject id of the user behind an administrative change.
        public string? Actor { get; set; }
    }

    public class AuditEventPageModel
    {
        public List<AuditEventModel> Items { get; set; } = [];
        public int PageSize { get; set; }
        public string? NextCursor { get; set; }
        public bool HasMore { get; set; }
        public int? TotalCount { get; set; }
        public bool TotalCountCapped { get; set; }
    }

    public class TransactionLineageModel
    {
        public Guid TransactionId { get; set; }
        public string? ExternalTransactionId { get; set; }
        public string Channel { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public Guid? InboxMessageId { get; set; }
        public DateTime? ReceivedAt { get; set; }
        public DateTime IngestedAt { get; set; }
        public List<AuditEventModel> Events { get; set; } = [];
    }

    public class AuditFilter
    {
        public string? Channel { get; set; }
        public string? EventType { get; set; }
        public string? SourceName { get; set; }
        public string? ExternalAccountId { get; set; }
        public Guid? InboxMessageId { get; set; }
        public Guid? TransactionId { get; set; }
        public string? ExternalTransactionId { get; set; }

        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        public string? Cursor { get; set; }
        public int PageSize { get; set; } = 50;
    }

    public static class AuditVocabulary
    {
        public static readonly IReadOnlyList<(string Group, string Type, string Label)> EventTypes =
        [
            ("Delivery", "inbound.received", "Received"),
            ("Delivery", "inbound.duplicate", "Duplicate delivery"),
            ("Delivery", "inbound.requeued", "Requeued"),
            ("Delivery", "inbound.rejected", "Rejected"),
            ("Delivery", "inbound.unauthorized", "Unauthorized"),
            ("Processing", "inbound.processed", "Processed"),
            ("Processing", "inbound.processing_failed", "Processing failed"),
            ("Processing", "inbound.dead_lettered", "Dead-lettered"),
            ("Transaction", "transaction.ingested", "Recorded in the ledger"),
            ("Transaction", "transaction.duplicate_skipped", "Duplicate skipped"),
            ("Transaction", "transaction.pending_skipped", "Pending notice (not recorded)"),
            ("Administration", "admin.source_created", "Bank created"),
            ("Administration", "admin.source_updated", "Bank details changed"),
            ("Administration", "admin.source_activated", "Bank activated"),
            ("Administration", "admin.source_deactivated", "Bank deactivated"),
            ("Administration", "admin.source_key_rotated", "Webhook key rotated"),
            ("Administration", "admin.source_signing_key_registered", "Signing key registered")
        ];

        // Event types written before the ledger became insert-only; labelled so history stays readable.
        private static readonly IReadOnlyDictionary<string, string> HistoricalLabels = new Dictionary<string, string>
        {
            ["transaction.settled"] = "Transaction settled (historical)",
            ["transaction.expired"] = "Pending expired (historical)"
        };

        public static readonly IReadOnlyList<string> Channels = ["webhook", "kafka", "admin", "unknown"];

        public static string Label(string eventType) =>
            EventTypes.FirstOrDefault(e => e.Type == eventType).Label
            ?? HistoricalLabels.GetValueOrDefault(eventType)
            ?? eventType;

        public static string BadgeClass(string eventType) => eventType switch
        {
            "inbound.received" or "transaction.ingested" or "transaction.settled" => "audit-good",
            "inbound.processed" => "audit-info",
            "inbound.duplicate" or "inbound.requeued" or "transaction.duplicate_skipped" or "inbound.processing_failed"
                or "transaction.pending_skipped" or "transaction.expired" => "audit-warn",
            "admin.source_key_rotated" or "admin.source_deactivated" or "admin.source_signing_key_registered" => "audit-warn",
            "admin.source_created" or "admin.source_updated" or "admin.source_activated" => "audit-info",
            "inbound.rejected" or "inbound.unauthorized" or "inbound.dead_lettered" => "audit-bad",
            _ => "audit-neutral"
        };

        public static string ChannelIcon(string channel) => channel switch
        {
            "webhook" => "bi-globe2",
            "kafka" => "bi-diagram-3",
            "admin" => "bi-person-gear",
            _ => "bi-question-circle"
        };
    }
}