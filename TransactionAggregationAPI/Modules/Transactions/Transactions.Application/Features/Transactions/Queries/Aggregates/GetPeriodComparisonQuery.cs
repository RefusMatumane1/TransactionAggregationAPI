using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Caching;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.Caching;
using Modules.Transactions.Application.Common.DTOs;
using SharedKernel.Common.ValueObjects;

namespace Modules.Transactions.Application.Features.Transactions.Queries.Aggregates
{
    public sealed record GetPeriodComparisonQuery(
        DateOnly From, DateOnly To, TransactionFilter Filter, string Currency = SupportedCurrency.Default)
        : IQuery<PeriodComparisonDto>, ICacheableQuery
    {
        public TimeSpan? CacheExpiration => TransactionCacheScopes.AggregatesExpiration;
        public string CacheScope => TransactionCacheScopes.Aggregates;
    }
}