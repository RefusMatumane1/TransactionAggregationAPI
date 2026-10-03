using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Caching;
using BuildingBlocks.Application.Pagination;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.Caching;
using Modules.Transactions.Application.Common.DTOs;
using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Application.Features.Transactions.Queries.GetTransactions
{
    public sealed record GetTransactionsQuery(TransactionFilter Filter) : IQuery<CursorPage<TransactionListItemDto>>, ICacheableQuery
    {
        public string? Cursor { get; init; }
        public int PageSize { get; init; } = 20;
        public bool IncludeTotal { get; init; }

        public TransactionCategory? Category { get; init; }
        public string? Currency { get; init; }
        public DateTime? FromDate { get; init; }
        public DateTime? ToDate { get; init; }
        public decimal? MinAmount { get; init; }
        public decimal? MaxAmount { get; init; }
        public string? SearchTerm { get; init; }

        public string? SortBy { get; init; }
        public bool SortDescending { get; init; } = true;

        public TimeSpan? CacheExpiration => TimeSpan.FromMinutes(5);

        public string CacheScope => TransactionCacheScopes.All;
    }
}