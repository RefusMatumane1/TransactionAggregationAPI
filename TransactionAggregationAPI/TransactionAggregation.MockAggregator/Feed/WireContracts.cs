using TransactionAggregation.MockAggregator.Catalog;

namespace TransactionAggregation.MockAggregator.Feed
{
    public sealed record DeliveryPayload(
        string ExternalAccountId, string Institution, IReadOnlyList<DeliveryItem> Transactions, int SchemaVersion = 2);

    public sealed record DeliveryItem(
        string Id,
        decimal Amount,
        string Currency,
        string Description,
        string? Category,
        string Date,
        string Status);

    public sealed record GeneratedTransaction(
        string Id,
        Merchant Merchant,
        decimal Amount,
        DateTime DateUtc,
        bool IsPending);

    public sealed record DeliveryBatch(MockAccount Account, string IdempotencyKey, DeliveryPayload Payload);
}