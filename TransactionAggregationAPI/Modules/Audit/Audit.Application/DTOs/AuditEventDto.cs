using Modules.Audit.Domain;

namespace Modules.Audit.Application.DTOs
{
    public sealed record AuditEventDto(
        Guid Id,
        string EventType,
        DateTime OccurredAt,
        DateTime RecordedAt,
        string Channel,
        string SourceName,
        string? ExternalAccountId,
        Guid? InboxMessageId,
        string? IdempotencyKey,
        Guid? CustomerId,
        Guid? TransactionId,
        string? ExternalTransactionId,
        string? Detail,
        IReadOnlyDictionary<string, string> Metadata,
        string? TraceId)
    {
        public static AuditEventDto From(AuditEvent e) => new(
            e.Id, e.EventType, e.OccurredAt, e.RecordedAt, e.Channel, e.SourceName,
            e.ExternalAccountId, e.InboxMessageId, e.IdempotencyKey, e.CustomerId,
            e.TransactionId, e.ExternalTransactionId, e.Detail, e.Metadata, e.TraceId);
    }

    public sealed record AuditEventPage(
        IReadOnlyList<AuditEventDto> Items,
        int PageNumber,
        int PageSize,
        int TotalCount)
    {
        public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    /// <summary>
    /// Everything known about where one transaction came from: its own ingestion event,
    /// plus every event of the delivery that carried it (receipt with channel metadata,
    /// retries, processing).
    /// </summary>
    public sealed record TransactionLineageDto(
        Guid TransactionId,
        string? ExternalTransactionId,
        string Channel,
        string SourceName,
        Guid? InboxMessageId,
        DateTime? ReceivedAt,
        DateTime IngestedAt,
        IReadOnlyList<AuditEventDto> Events);
}