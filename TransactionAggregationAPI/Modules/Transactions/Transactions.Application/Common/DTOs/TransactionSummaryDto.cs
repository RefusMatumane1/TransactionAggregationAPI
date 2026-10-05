using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Common.DTOs
{
    public sealed record TransactionSummaryDto(
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal NetBalance,
        Dictionary<TransactionCategory, decimal> SpendingByCategory,
        int TotalTransactions,
        IReadOnlyList<MonthlySummaryDto> MonthlySummaries,
        string Currency,
        DateTime? AsOf);

    public sealed record MonthlySummaryDto(
        int Year,
        int Month,
        string MonthName,
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal NetBalance,
        int TransactionCount);
}