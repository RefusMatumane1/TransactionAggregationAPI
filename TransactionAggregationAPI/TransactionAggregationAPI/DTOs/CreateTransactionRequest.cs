namespace TransactionAggregationAPI.DTOs
{
    public sealed record CreateTransactionRequest(
        decimal Amount,
        string Currency,
        DateTime TransactionDate,
        string Description,
        string SourceSystem,
        Guid? AccountId = null);
}
