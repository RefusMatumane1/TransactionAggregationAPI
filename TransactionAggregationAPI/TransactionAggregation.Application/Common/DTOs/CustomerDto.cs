namespace TransactionAggregation.Application.Common.DTOs
{
    public record CustomerDto(
        Guid Id,
        string Email,
        string Name,
        DateTime CreatedAt,
        DateTime? UpdatedAt = null);

    public record CustomerWithTransactionsDto(
        Guid Id,
        string Email,
        string Name,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        IEnumerable<TransactionDto> Transactions,
        int TotalTransactions,
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal NetBalance);
}
