using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Common.Outbox
{
    public static class OutboxMessageTypes
    {
        public const string TransactionCreated = "TransactionCreated";
        public const string TransactionCategorized = "TransactionCategorized";
        public const string TransactionSynced = "TransactionSynced";
        public const string DuplicateInboundDetected = "DuplicateInboundDetected";
        public const string TransactionsExpired = "TransactionsExpired";
    }

    /// <summary>
    /// One message per expiry batch: expired pending amounts leave the customers'
    /// pending/available figures, so their cached lists and summaries must be dropped.
    /// </summary>
    public sealed record TransactionsExpiredOutboxPayload(IReadOnlyList<Guid> CustomerIds, int Count);

    public sealed record TransactionCreatedOutboxPayload(Guid TransactionId, Guid CustomerId);

    public sealed record TransactionCategorizedOutboxPayload(
        Guid TransactionId,
        Guid CustomerId,
        TransactionCategory OldCategory,
        TransactionCategory NewCategory,
        bool IsAutoCategorized);

    public sealed record TransactionSyncedOutboxPayload(Guid TransactionId, Guid CustomerId, string SyncSource);

    /// <summary>
    /// Level is "message" when a whole delivery (webhook call / Kafka record) replayed one
    /// already in the inbox, or "transaction" when individual transactions inside a delivery
    /// were already stored. Either way the duplicates were dropped and everything else was
    /// processed — this is a notification, not a failure.
    /// </summary>
    public sealed record DuplicateInboundDetectedOutboxPayload(
        string Level,
        string SourceName,
        string ExternalAccountId,
        IReadOnlyList<string> DuplicateExternalIds,
        Guid? InboxMessageId,
        Guid? CustomerId,
        DateTime DetectedAt);
}