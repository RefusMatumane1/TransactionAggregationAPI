using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Application.Querying;
using Modules.Currency.Contracts;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransactions;
using Modules.Transactions.Contracts;

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
            new(new AccountFilter(access, Institution, ExternalAccountId))
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
}