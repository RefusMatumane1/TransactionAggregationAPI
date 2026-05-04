using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Common.DTOs
{
    public record TransactionSummaryDto(
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal NetBalance,
        Dictionary<TransactionCategory, decimal> SpendingByCategory,
        int TotalTransactions,
        IReadOnlyList<MonthlySummaryDto> MonthlySummaries);

    public record MonthlySummaryDto(
        int Year,
        int Month,
        string MonthName,
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal NetBalance,
        int TransactionCount);
}
