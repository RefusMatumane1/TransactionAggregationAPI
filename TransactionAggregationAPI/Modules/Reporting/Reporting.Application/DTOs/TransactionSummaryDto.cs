using Modules.Transactions.Contracts;

namespace Modules.Reporting.Application.DTOs
{
    public sealed record TransactionSummaryDto(
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal NetBalance,
        Dictionary<TransactionCategory, decimal> SpendingByCategory,
        int TotalTransactions,
        IReadOnlyList<MonthlySummaryDto> MonthlySummaries,
        string Currency,
        DateTime? AsOf,
        ConversionDto? Conversion = null);

    public sealed record MonthlySummaryDto(
        int Year,
        int Month,
        string MonthName,
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal NetBalance,
        int TransactionCount);
}