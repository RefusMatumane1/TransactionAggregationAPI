using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Common.Outbox
{
    /// <summary>
    /// Small, JSON-serializable payloads for OutboxMessage rows — deliberately not the domain
    /// entity itself (not safely serializable/re-hydratable) and not the in-process domain event
    /// classes (those carry a live Transaction reference). Each carries just enough to either
    /// act directly (CustomerId, for cache-key invalidation) or re-fetch the Transaction the
    /// dispatcher needs for the existing IAnalyticsService/INotificationService calls, which
    /// still take the full entity — those interfaces are unchanged by this.
    /// </summary>
    public static class OutboxMessageTypes
    {
        public const string TransactionCreated = "TransactionCreated";
        public const string TransactionCategorized = "TransactionCategorized";
        public const string TransactionSynced = "TransactionSynced";
    }

    public sealed record TransactionCreatedOutboxPayload(Guid TransactionId, Guid CustomerId);

    public sealed record TransactionCategorizedOutboxPayload(
        Guid TransactionId,
        Guid CustomerId,
        TransactionCategory OldCategory,
        TransactionCategory NewCategory,
        bool IsAutoCategorized);

    public sealed record TransactionSyncedOutboxPayload(Guid TransactionId, Guid CustomerId, string SyncSource);
}
