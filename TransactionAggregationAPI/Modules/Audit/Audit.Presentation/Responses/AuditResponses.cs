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
        Guid? TransactionId,
        string? ExternalTransactionId,
        string? Detail,
        IReadOnlyDictionary<string, string> Metadata,
        string? TraceId,
        string? Actor)
    {
        internal static AuditEventResponse From(AuditEventDto e) => new(
            e.Id, e.EventType, e.OccurredAt, e.RecordedAt, e.Channel, e.SourceName,
            e.ExternalAccountId, e.InboxMessageId, e.IdempotencyKey,
            e.TransactionId, e.ExternalTransactionId, e.Detail, e.Metadata, e.TraceId, e.Actor);
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