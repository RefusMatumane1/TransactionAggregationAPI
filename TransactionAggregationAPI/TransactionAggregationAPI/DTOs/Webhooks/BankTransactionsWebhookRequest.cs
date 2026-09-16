namespace TransactionAggregationAPI.DTOs.Webhooks
{
    /// <summary>Payload the account aggregator posts to push new transactions for one linked
    /// account — see WebhookEndpoints.</summary>
    public sealed record BankTransactionsWebhookRequest(
        string ExternalAccountId,
        IReadOnlyList<BankTransactionWebhookItem> Transactions);

    public sealed record BankTransactionWebhookItem(
        string Id,
        decimal Amount,
        string Currency,
        string Description,
        string? Category,
        DateTime Date);
}
