using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Common.DTOs
{
    /// <summary>
    /// Money figures (income, expenses, net, by category, by month) count booked (Settled)
    /// transactions only — see TransactionTotals. Pending authorisations are reported
    /// separately in the Pending* fields.
    /// </summary>
    /// <param name="TotalTransactions">Every transaction in the period, whatever its status.</param>
    /// <param name="CompletedTransactions">Booked (Settled) transactions — the ones the money figures are built from.</param>
    /// <param name="PendingTransactions">Pending authorisations not yet posted by the bank.</param>
    public record TransactionSummaryDto(
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal NetBalance,
        Dictionary<TransactionCategory, decimal> SpendingByCategory,
        int TotalTransactions,
        IReadOnlyList<MonthlySummaryDto> MonthlySummaries,
        int CompletedTransactions = 0,
        int PendingTransactions = 0,
        decimal PendingIncome = 0,
        decimal PendingExpenses = 0);

    public record MonthlySummaryDto(
        int Year,
        int Month,
        string MonthName,
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal NetBalance,
        int TransactionCount);
}