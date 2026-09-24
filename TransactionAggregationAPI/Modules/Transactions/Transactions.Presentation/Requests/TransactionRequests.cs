using Modules.Transactions.Application.Features.Transactions.Commands.CategorizeTransaction;
using Modules.Transactions.Application.Features.Transactions.Queries.ExportTransactions;
using Modules.Transactions.Application.Features.Transactions.Queries.GetCustomerWithTransactions;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactions;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactionSummary;
using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Presentation.Requests
{
    public sealed record CategorizeTransactionRequest(TransactionCategory Category)
    {
        internal CategorizeTransactionCommand ToCommand(Guid transactionId, Guid customerId) =>
            new(transactionId, customerId, Category);
    }

    /// <summary>Query string of GET /customers/{customerId}/transactions.</summary>
    public sealed record GetCustomerWithTransactionsRequest(
        DateTime? StartDate,
        DateTime? EndDate,
        TransactionCategory? Category,
        int Page = 1,
        int PageSize = 20)
    {
        internal GetCustomerWithTransactionsQuery ToQuery(Guid customerId) =>
            new(customerId, StartDate, EndDate, Category, Page, PageSize);
    }

    /// <summary>Query string of GET /customers/{customerId}/transactions/filter; every filter is optional.</summary>
    public sealed record FilterTransactionsRequest(
        int PageNumber = 1,
        int PageSize = 20,
        TransactionCategory? Category = null,
        TransactionStatus? Status = null,
        DateTime? FromDate = null,
        DateTime? ToDate = null,
        decimal? MinAmount = null,
        decimal? MaxAmount = null,
        string? SearchTerm = null,
        string? Source = null,
        string? SortBy = null,
        bool SortDescending = true)
    {
        internal GetTransactionsQuery ToQuery(Guid customerId) => new()
        {
            CustomerId = customerId,
            PageNumber = PageNumber,
            PageSize = PageSize,
            Category = Category,
            Status = Status,
            FromDate = FromDate,
            ToDate = ToDate,
            MinAmount = MinAmount,
            MaxAmount = MaxAmount,
            SearchTerm = SearchTerm,
            Source = Source,
            SortBy = SortBy,
            SortDescending = SortDescending
        };
    }

    /// <summary>Query string of the summary endpoint; the period defaults to the last 12 months.</summary>
    public sealed record TransactionSummaryRequest(DateTime? StartDate = null, DateTime? EndDate = null)
    {
        internal GetTransactionSummaryQuery ToQuery(Guid customerId) =>
            new(customerId, StartDate ?? DateTime.UtcNow.AddMonths(-12), EndDate ?? DateTime.UtcNow);
    }

    public sealed record ExportTransactionsRequest(
        DateTime? FromDate = null,
        DateTime? ToDate = null,
        TransactionCategory? Category = null)
    {
        internal ExportTransactionsQuery ToQuery(Guid customerId) => new()
        {
            CustomerId = customerId,
            FromDate = FromDate,
            ToDate = ToDate,
            Category = Category,
            Format = "csv"
        };
    }
}