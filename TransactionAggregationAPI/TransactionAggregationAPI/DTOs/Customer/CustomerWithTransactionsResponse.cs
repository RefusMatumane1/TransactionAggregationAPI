namespace TransactionAggregationAPI.DTOs.Customer
{
    public sealed record CustomerWithTransactionsResponse(
        Guid Id,
        string Email,
        string Name,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        IEnumerable<TransactionResponse> Transactions,
        int TotalTransactions,
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal NetBalance);
}
