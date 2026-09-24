using Modules.Audit.Application.Features.SearchAuditEvents;

namespace Modules.Audit.Presentation.Requests
{
    /// <summary>Query-string filters for the audit search; every filter is optional.</summary>
    public sealed record SearchAuditEventsRequest(
        string? Channel = null,
        string? SourceName = null,
        string? EventType = null,
        string? ExternalAccountId = null,
        Guid? InboxMessageId = null,
        Guid? CustomerId = null,
        Guid? TransactionId = null,
        string? ExternalTransactionId = null,
        DateTime? From = null,
        DateTime? To = null,
        int PageNumber = 1,
        int PageSize = 50)
    {
        internal SearchAuditEventsQuery ToQuery() => new()
        {
            Channel = Channel,
            SourceName = SourceName,
            EventType = EventType,
            ExternalAccountId = ExternalAccountId,
            InboxMessageId = InboxMessageId,
            CustomerId = CustomerId,
            TransactionId = TransactionId,
            ExternalTransactionId = ExternalTransactionId,
            From = From,
            To = To,
            PageNumber = PageNumber,
            PageSize = PageSize
        };
    }
}