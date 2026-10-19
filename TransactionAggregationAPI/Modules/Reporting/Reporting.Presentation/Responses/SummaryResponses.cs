using Modules.Reporting.Application.DTOs;
using Modules.Transactions.Contracts;

namespace Modules.Reporting.Presentation.Responses
{
    public sealed record TransactionSummaryResponse(
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal NetBalance,
        Dictionary<TransactionCategory, decimal> SpendingByCategory,
        int TotalTransactions,
        IReadOnlyList<MonthlySummaryResponse> MonthlySummaries,
        string Currency,
        DateTime? AsOf,
        ConversionResponse? Conversion = null)
    {
        internal static TransactionSummaryResponse From(TransactionSummaryDto summary) => new(
            summary.TotalIncome,
            summary.TotalExpenses,
            summary.NetBalance,
            summary.SpendingByCategory,
            summary.TotalTransactions,
            summary.MonthlySummaries.Select(MonthlySummaryResponse.From).ToList(),
            summary.Currency,
            summary.AsOf,
            ConversionResponse.From(summary.Conversion));
    }

    public sealed record MonthlySummaryResponse(
        int Year,
        int Month,
        string MonthName,
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal NetBalance,
        int TransactionCount)
    {
        internal static MonthlySummaryResponse From(MonthlySummaryDto month) => new(
            month.Year, month.Month, month.MonthName, month.TotalIncome,
            month.TotalExpenses, month.NetBalance, month.TransactionCount);
    }
}
