namespace TransactionAggregationUI.Models.Audit;

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
    public Guid? CustomerId { get; set; }
    public Guid? TransactionId { get; set; }
    public string? ExternalTransactionId { get; set; }
    public string? Detail { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public string? TraceId { get; set; }
}

public class AuditEventPageModel
{
    public List<AuditEventModel> Items { get; set; } = [];
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
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

    /// <summary>Local time, as entered in the datetime-local inputs; converted to UTC for the API.</summary>
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>Display names and styling for the API's event-type / channel identifiers.</summary>
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
        ("Transaction", "transaction.ingested", "Transaction ingested"),
        ("Transaction", "transaction.settled", "Transaction settled"),
        ("Transaction", "transaction.expired", "Pending expired"),
        ("Transaction", "transaction.duplicate_skipped", "Duplicate skipped")
    ];

    public static readonly IReadOnlyList<string> Channels = ["webhook", "kafka", "system", "unknown"];

    public static string Label(string eventType) =>
        EventTypes.FirstOrDefault(e => e.Type == eventType).Label ?? eventType;

    /// <summary>Green = data accepted, blue = processing progress, amber = replay/retry, red = refused or given up on.</summary>
    public static string BadgeClass(string eventType) => eventType switch
    {
        "inbound.received" or "transaction.ingested" or "transaction.settled" => "audit-good",
        "inbound.processed" => "audit-info",
        "inbound.duplicate" or "inbound.requeued" or "transaction.duplicate_skipped" or "inbound.processing_failed"
            or "transaction.expired" => "audit-warn",
        "inbound.rejected" or "inbound.unauthorized" or "inbound.dead_lettered" => "audit-bad",
        _ => "audit-neutral"
    };

    public static string ChannelIcon(string channel) => channel switch
    {
        "webhook" => "bi-globe2",
        "kafka" => "bi-diagram-3",
        "system" => "bi-gear",
        _ => "bi-question-circle"
    };
}