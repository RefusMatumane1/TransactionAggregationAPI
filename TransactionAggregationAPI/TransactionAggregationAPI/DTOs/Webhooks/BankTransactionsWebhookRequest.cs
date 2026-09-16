namespace TransactionAggregationAPI.DTOs.Webhooks
{
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