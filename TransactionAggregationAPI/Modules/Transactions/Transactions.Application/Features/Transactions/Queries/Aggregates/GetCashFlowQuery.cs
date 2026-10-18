using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Caching;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.Caching;
using Modules.Transactions.Application.Common.DTOs;

namespace Modules.Transactions.Application.Features.Transactions.Queries.Aggregates
{
    public sealed record GetCashFlowQuery(
        DateOnly From,
        DateOnly To,
        TransactionFilter Filter,
        TimeGranularity Granularity = TimeGranularity.Month,
        string? Currency = null,
        string? ConvertTo = null,
        string? RatesVersion = null) : IQuery<CashFlowSeriesDto>, ICacheableQuery
    {
        public TimeSpan? CacheExpiration => TransactionCacheScopes.AggregatesExpiration;
        public string CacheScope => TransactionCacheScopes.Aggregates;
    }
}