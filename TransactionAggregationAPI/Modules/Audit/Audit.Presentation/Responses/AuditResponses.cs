using Modules.Audit.Application.DTOs;

namespace Modules.Audit.Presentation.Responses
{
    public sealed record AuditEventResponse(
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
        internal static AuditEventResponse From(AuditEventDto e) => new(
            e.Id, e.EventType, e.OccurredAt, e.RecordedAt, e.Channel, e.SourceName,
            e.ExternalAccountId, e.InboxMessageId, e.IdempotencyKey, e.CustomerId,
            e.TransactionId, e.ExternalTransactionId, e.Detail, e.Metadata, e.TraceId);
    }

    public sealed record AuditEventPageResponse(
        IReadOnlyList<AuditEventResponse> Items,
        int PageNumber,
        int PageSize,
        int TotalCount,
        int TotalPages)
    {
        internal static AuditEventPageResponse From(AuditEventPage page) => new(
            page.Items.Select(AuditEventResponse.From).ToList(),
            page.PageNumber,
            page.PageSize,
            page.TotalCount,
            page.TotalPages);
    }

    public sealed record TransactionLineageResponse(
        Guid TransactionId,
        string? ExternalTransactionId,
        string Channel,
        string SourceName,
        Guid? InboxMessageId,
        DateTime? ReceivedAt,
        DateTime IngestedAt,
        IReadOnlyList<AuditEventResponse> Events)
    {
        internal static TransactionLineageResponse From(TransactionLineageDto lineage) => new(
            lineage.TransactionId,
            lineage.ExternalTransactionId,
            lineage.Channel,
            lineage.SourceName,
            lineage.InboxMessageId,
            lineage.ReceivedAt,
            lineage.IngestedAt,
            lineage.Events.Select(AuditEventResponse.From).ToList());
    }
}