using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Common.DTOs
{
    public record TransactionSummaryDto(
        decimal TotalIncome,
        decimal TotalExpenses,
        Dictionary<TransactionCategory, decimal> SpendingByCategory,
        int TotalTransactions);
}
