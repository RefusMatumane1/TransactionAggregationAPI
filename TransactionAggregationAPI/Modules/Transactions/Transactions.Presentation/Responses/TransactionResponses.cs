using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Presentation.Responses
{
    public sealed record TransactionResponse(
        Guid Id,
        Guid CustomerId,
        Guid? AccountId,
        decimal Amount,
        string Currency,
        DateTime TransactionDate,
        string Description,
        TransactionCategory Category,
        TransactionStatus Status,
        string SourceSystem,
        DateTime CreatedAt,
        DateTime? UpdatedAt = null)
    {
        // TransactionDto carries no CreatedAt/UpdatedAt, so these have always been sent as
        // their defaults; they stay on the contract so existing clients keep deserializing.
        internal static TransactionResponse From(TransactionDto transaction) => new(
            transaction.Id,
            transaction.CustomerId,
            transaction.AccountId,
            transaction.Amount,
            transaction.Currency,
            transaction.TransactionDate,
            transaction.Description,
            transaction.Category,
            transaction.Status,
            transaction.SourceSystem,
            CreatedAt: default,
            UpdatedAt: null);
    }

    /// <summary>One row of the filterable transaction list, with display-ready fields precomputed.</summary>
    public sealed record TransactionListItemResponse(
        Guid Id,
        Guid CustomerId,
        Guid? AccountId,
        decimal Amount,
        string Currency,
        string FormattedAmount,
        string Description,
        TransactionCategory Category,
        string CategoryName,
        TransactionStatus Status,
        string StatusName,
        string Source,
        DateTime Date,
        DateTime CreatedAt,
        Dictionary<string, string> Metadata,
        bool IsExpense,
        bool IsIncome,
        string Age)
    {
        internal static TransactionListItemResponse From(TransactionListItemDto t) => new(
            t.Id, t.CustomerId, t.AccountId, t.Amount, t.Currency,
            FormattedAmount: $"{t.Currency} {t.Amount:N2}",
            t.Description,
            t.Category, CategoryName: t.Category.ToString(),
            t.Status, StatusName: t.Status.ToString(),
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

    public sealed record CustomerWithTransactionsResponse(
        Guid Id,
        string Email,
        string Name,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        IEnumerable<TransactionResponse> Transactions,
        int TotalTransactions,
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal NetBalance,
        decimal PendingIncome,
        decimal PendingExpenses)
    {
        internal static CustomerWithTransactionsResponse From(CustomerWithTransactionsDto customer) => new(
            customer.Id,
            customer.Email,
            customer.Name,
            customer.CreatedAt,
            customer.UpdatedAt,
            customer.Transactions.Select(TransactionResponse.From).ToList(),
            customer.TotalTransactions,
            customer.TotalIncome,
            customer.TotalExpenses,
            customer.NetBalance,
            customer.PendingIncome,
            customer.PendingExpenses);
    }

    /// <summary>
    /// Money figures count booked (Settled) transactions only; pending authorisations are
    /// reported separately in the Pending* fields.
    /// </summary>
    public sealed record TransactionSummaryResponse(
        decimal TotalIncome,
        decimal TotalExpenses,
        decimal NetBalance,
        Dictionary<TransactionCategory, decimal> SpendingByCategory,
        int TotalTransactions,
        IReadOnlyList<MonthlySummaryResponse> MonthlySummaries,
        int CompletedTransactions,
        int PendingTransactions,
        decimal PendingIncome,
        decimal PendingExpenses)
    {
        internal static TransactionSummaryResponse From(TransactionSummaryDto summary) => new(
            summary.TotalIncome,
            summary.TotalExpenses,
            summary.NetBalance,
            summary.SpendingByCategory,
            summary.TotalTransactions,
            summary.MonthlySummaries.Select(MonthlySummaryResponse.From).ToList(),
            summary.CompletedTransactions,
            summary.PendingTransactions,
            summary.PendingIncome,
            summary.PendingExpenses);
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

    /// <summary>
    /// Acknowledgement of an inbound delivery. A duplicate is still a 202: the sender's retry
    /// succeeded from its point of view, and an error status would only make it retry again.
    /// </summary>
    public sealed record WebhookReceiptResponse(Guid InboxMessageId, bool IsDuplicate, bool Requeued);
}