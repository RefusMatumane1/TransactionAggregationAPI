using BuildingBlocks.Application.Abstractions.Authentication;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.Currency;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactions;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactionSummary;
using Modules.Transactions.Domain.Enums;

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
        bool SortDescending = true,
        string? ConvertTo = null)
    {
        internal GetTransactionsQuery ToQuery(InstitutionAccess access, ICurrencyCatalog catalog) =>
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
                SortDescending = SortDescending,
                ConvertTo = ConvertTo,
                RatesVersion = ConvertTo is null ? null : catalog.Version
            };
    }

    public sealed record TransactionSummaryRequest(
        DateTime? StartDate = null,
        DateTime? EndDate = null,
        string? Institution = null,
        string? ExternalAccountId = null,
        string? Currency = null,
        string? ConvertTo = null)
    {
        internal GetTransactionSummaryQuery ToQuery(InstitutionAccess access, TimeProvider time, ICurrencyCatalog catalog)
        {
            var now = time.GetUtcNow().UtcDateTime;
            return new(StartDate ?? now.AddMonths(-12), EndDate ?? now,
                new TransactionFilter(access, Institution, ExternalAccountId), Currency ?? catalog.BaseCurrency,
                ConvertTo, ConvertTo is null ? null : catalog.Version);
        }
    }
}