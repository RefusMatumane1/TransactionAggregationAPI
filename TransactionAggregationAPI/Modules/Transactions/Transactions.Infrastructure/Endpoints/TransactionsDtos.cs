using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Infrastructure.Endpoints
{
    public sealed record CategorizeTransactionRequest(
        TransactionCategory Category);

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
        DateTime? UpdatedAt = null);

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
        decimal NetBalance);

    public sealed record PaginationQueryParams(
        DateTime? StartDate,
        DateTime? EndDate,
        TransactionCategory? Category,
        int Page = 1,
        int PageSize = 20);

    public sealed record BankTransactionsWebhookRequest(
        string ExternalAccountId,
        IReadOnlyList<BankTransactionWebhookItem> Transactions);

    public sealed record BankTransactionWebhookItem(
        string Id,
        decimal Amount,
        string Currency,
        string Description,
        string? Category,
        DateTime Date);
}
