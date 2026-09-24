using TransactionAggregation.MockAggregator.Catalog;

namespace TransactionAggregation.MockAggregator.Feed;

/// <summary>
/// The aggregator's push format — the documented body of the application's bank-transactions
/// webhook, and the value of its Kafka records. Declared here rather than borrowed from the
/// application, because an outside sender only has the contract, not the code.
/// </summary>
public sealed record DeliveryPayload(string ExternalAccountId, IReadOnlyList<DeliveryItem> Transactions);

/// <param name="Date">A string, so each bank can write its timestamps its own way.</param>
/// <param name="Status">"pending" or "posted".</param>
public sealed record DeliveryItem(
    string Id,
    decimal Amount,
    string Currency,
    string Description,
    string? Category,
    string Date,
    string Status);

/// <summary>A transaction the feed has decided happened, before any bank formats it.</summary>
/// <param name="Amount">Negative is money out.</param>
public sealed record GeneratedTransaction(
    string Id,
    Merchant Merchant,
    decimal Amount,
    DateTime DateUtc,
    bool IsPending);

/// <param name="IdempotencyKey">Reused when a batch is re-sent, so the receiver recognises the replay.</param>
public sealed record DeliveryBatch(MockAccount Account, string IdempotencyKey, DeliveryPayload Payload);