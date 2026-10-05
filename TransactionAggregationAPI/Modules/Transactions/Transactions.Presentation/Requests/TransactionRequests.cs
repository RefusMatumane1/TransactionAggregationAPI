using BuildingBlocks.Application.Abstractions.Authentication;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactions;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactionSummary;
using Modules.Transactions.Domain.Enums;
using SharedKernel.Common.ValueObjects;

namespace Modules.Transactions.Presentation.Requests
{
    public sealed record FilterTransactionsRequest(
        string? Cursor = null,
        int PageSize = 20,
        bool IncludeTotal = false,
        TransactionCategory? Category = null,
        string? Currency = null,
        DateTime? FromDate = null,
        DateTime? ToDate = null,
        decimal? MinAmount = null,
        decimal? MaxAmount = null,
        string? SearchTerm = null,
        string? Institution = null,
        string? ExternalAccountId = null,
        string? SortBy = null,
        bool SortDescending = true)
    {
        internal GetTransactionsQuery ToQuery(InstitutionAccess access) =>
            new(new TransactionFilter(access, Institution, ExternalAccountId))
            {
                Cursor = Cursor,
                PageSize = PageSize,
                IncludeTotal = IncludeTotal,
                Category = Category,
                Currency = Currency,
                FromDate = FromDate,
                ToDate = ToDate,
                MinAmount = MinAmount,
                MaxAmount = MaxAmount,
                SearchTerm = SearchTerm,
                SortBy = SortBy,
                SortDescending = SortDescending
            };
    }

    public sealed record TransactionSummaryRequest(
        DateTime? StartDate = null,
        DateTime? EndDate = null,
        string? Institution = null,
        string? ExternalAccountId = null,
        string Currency = SupportedCurrency.Default)
    {
        internal GetTransactionSummaryQuery ToQuery(InstitutionAccess access, TimeProvider time)
        {
            var now = time.GetUtcNow().UtcDateTime;
            return new(StartDate ?? now.AddMonths(-12), EndDate ?? now,
                new TransactionFilter(access, Institution, ExternalAccountId), Currency);
        }
    }
}