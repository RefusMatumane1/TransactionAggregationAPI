using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Presentation.Responses
{
    public sealed record TransactionResponse(
        Guid Id,
        string Institution,
        string ExternalAccountId,
        string ExternalTransactionId,
        decimal Amount,
        string Currency,
        DateTime TransactionDate,
        string Description,
        TransactionCategory Category,
        DateTime RecordedAt,
        IReadOnlyDictionary<string, string> Metadata)
    {
        internal static TransactionResponse From(TransactionDto t) => new(
            t.Id, t.Institution, t.ExternalAccountId, t.ExternalTransactionId,
            t.Amount, t.Currency, t.TransactionDate, t.Description, t.Category, t.RecordedAt, t.Metadata);
    }

    public sealed record TransactionListItemResponse(
        Guid Id,
        string ExternalAccountId,
        decimal Amount,
        string Currency,
        string FormattedAmount,
        string Description,
        TransactionCategory Category,
        string CategoryName,
        string Source,
        DateTime Date,
        DateTime CreatedAt,
        Dictionary<string, string> Metadata,
        bool IsExpense,
        bool IsIncome,
        string Age)
    {
        internal static TransactionListItemResponse From(TransactionListItemDto t) => new(
            t.Id, t.ExternalAccountId, t.Amount, t.Currency,
            FormattedAmount: $"{t.Currency} {t.Amount:N2}",
            t.Description,
            t.Category, CategoryName: t.Category.ToString(),
            t.Source, t.Date, t.CreatedAt, t.Metadata,
            IsExpense: t.Amount < 0,
            IsIncome: t.Amount > 0,
            Age: DescribeAge(t.Date));

        private static string DescribeAge(DateTime date)
        {
            var days = (DateTime.UtcNow - date).Days;
            return days switch
            {
                0 => "Today",
                1 => "Yesterday",
                < 7 => $"{days} days ago",
                < 30 => $"{days / 7} weeks ago",
                < 365 => $"{days / 30} months ago",
                _ => $"{days / 365} years ago"
            };
        }
    }

    public sealed record TransactionSummaryResponse(
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal NetBalance,
        Dictionary<TransactionCategory, decimal> SpendingByCategory,
        int TotalTransactions,
        IReadOnlyList<MonthlySummaryResponse> MonthlySummaries,
        string Currency,
        DateTime? AsOf)
    {
        internal static TransactionSummaryResponse From(TransactionSummaryDto summary) => new(
            summary.TotalIncome,
            summary.TotalExpenses,
            summary.NetBalance,
            summary.SpendingByCategory,
            summary.TotalTransactions,
            summary.MonthlySummaries.Select(MonthlySummaryResponse.From).ToList(),
            summary.Currency,
            summary.AsOf);
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

    public sealed record WebhookReceiptResponse(Guid InboxMessageId, bool IsDuplicate, bool Requeued);
}