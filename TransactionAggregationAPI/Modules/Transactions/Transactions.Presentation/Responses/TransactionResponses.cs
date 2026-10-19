using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Contracts;

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
        IReadOnlyDictionary<string, string> Metadata,
        decimal? ConvertedAmount = null,
        string? ConvertedCurrency = null)
    {
        internal static TransactionResponse From(TransactionDto t) => new(
            t.Id, t.Institution, t.ExternalAccountId, t.ExternalTransactionId,
            t.Amount, t.Currency, t.TransactionDate, t.Description, t.Category, t.RecordedAt, t.Metadata,
            t.ConvertedAmount, t.ConvertedCurrency);
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
        string Age,
        decimal? ConvertedAmount = null,
        string? ConvertedCurrency = null)
    {
        internal static TransactionListItemResponse From(TransactionListItemDto t) => new(
            t.Id, t.ExternalAccountId, t.Amount, t.Currency,
            FormattedAmount: $"{t.Currency} {t.Amount:N2}",
            t.Description,
            t.Category, CategoryName: t.Category.ToString(),
            t.Source, t.Date, t.CreatedAt, t.Metadata,
            IsExpense: t.Amount < 0,
            IsIncome: t.Amount > 0,
            Age: DescribeAge(t.Date),
            ConvertedAmount: t.ConvertedAmount,
            ConvertedCurrency: t.ConvertedCurrency);

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

    public sealed record WebhookReceiptResponse(Guid InboxMessageId, bool IsDuplicate, bool Requeued);
}