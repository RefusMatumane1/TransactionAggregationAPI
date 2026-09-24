namespace Modules.Transactions.Application.Common.DTOs
{
    public sealed record CustomerWithTransactionsDto(
        Guid Id,
        string Email,
        string Name,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        IEnumerable<TransactionDto> Transactions,
        int TotalTransactions,
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal NetBalance,
        decimal PendingIncome = 0,
        decimal PendingExpenses = 0);
}