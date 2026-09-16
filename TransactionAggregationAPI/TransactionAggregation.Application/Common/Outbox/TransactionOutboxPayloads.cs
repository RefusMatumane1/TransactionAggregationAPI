using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Common.Outbox
{
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