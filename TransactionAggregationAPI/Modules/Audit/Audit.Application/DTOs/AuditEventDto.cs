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
        Guid? TransactionId,
        string? ExternalTransactionId,
        string? Detail,
        IReadOnlyDictionary<string, string> Metadata,
        string? TraceId,
        string? Actor)
    {
        public static AuditEventDto From(AuditEvent e) => new(
            e.Id, e.EventType, e.OccurredAt, e.RecordedAt, e.Channel, e.SourceName,
            e.ExternalAccountId, e.InboxMessageId, e.IdempotencyKey,
            e.TransactionId, e.ExternalTransactionId, e.Detail, e.Metadata, e.TraceId, e.Actor);
    }

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