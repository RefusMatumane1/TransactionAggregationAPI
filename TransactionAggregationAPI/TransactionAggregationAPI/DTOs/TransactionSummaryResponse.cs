using TransactionAggregation.Domain.Enums;

namespace TransactionAggregationAPI.DTOs
{
    public sealed record TransactionSummaryResponse(
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal NetBalance,
        Dictionary<TransactionCategory, decimal> SpendingByCategory,
        Dictionary<string, decimal> SpendingByMonth,
        int TotalTransactions,
        int CompletedTransactions,
        int PendingTransactions,
        TransactionPeriod Period);

    public sealed record TransactionPeriod(
    DateTime StartDate,
    DateTime EndDate,
    int Days);
}
