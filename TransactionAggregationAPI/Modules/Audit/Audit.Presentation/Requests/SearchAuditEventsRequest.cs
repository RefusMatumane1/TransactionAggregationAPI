using Modules.Audit.Application.Features.SearchAuditEvents;

namespace Modules.Audit.Presentation.Requests
{
    public sealed record SearchAuditEventsRequest(
        string? Channel = null,
        string? SourceName = null,
        string? EventType = null,
        string? ExternalAccountId = null,
        Guid? InboxMessageId = null,
        Guid? TransactionId = null,
        string? ExternalTransactionId = null,
        string? Actor = null,
        DateTime? From = null,
        DateTime? To = null,
        string? Cursor = null,
        int PageSize = 50,
        bool IncludeTotal = false)
    {
        internal SearchAuditEventsQuery ToQuery() => new()
        {
            Channel = Channel,
            SourceName = SourceName,
            EventType = EventType,
            ExternalAccountId = ExternalAccountId,
            InboxMessageId = InboxMessageId,
            TransactionId = TransactionId,
            ExternalTransactionId = ExternalTransactionId,
            Actor = Actor,
            From = From,
            To = To,
            Cursor = Cursor,
            PageSize = PageSize,
            IncludeTotal = IncludeTotal
        };
    }
}