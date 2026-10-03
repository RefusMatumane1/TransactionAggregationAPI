namespace Modules.Audit.Contracts
{
    public sealed record AuditEventRecord(
        Guid EventId,
        string EventType,
        DateTime OccurredAt,
        string Channel,
        string SourceName,
        string? ExternalAccountId = null,
        Guid? InboxMessageId = null,
        string? IdempotencyKey = null,
        Guid? TransactionId = null,
        string? ExternalTransactionId = null,
        string? Detail = null,
        IReadOnlyDictionary<string, string>? Metadata = null,
        string? TraceId = null,
        string? Actor = null);
}