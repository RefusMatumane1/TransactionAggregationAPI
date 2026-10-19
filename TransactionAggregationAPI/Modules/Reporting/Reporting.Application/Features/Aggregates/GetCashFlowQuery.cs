using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Caching;
using BuildingBlocks.Application.Querying;
using Modules.Reporting.Application.Aggregation;
using Modules.Reporting.Application.DTOs;

namespace Modules.Reporting.Application.Features.Aggregates
{
    public sealed record GetCashFlowQuery(
        DateOnly From,
        DateOnly To,
        AccountFilter Filter,
        TimeGranularity Granularity = TimeGranularity.Month,
        string? Currency = null,
        string? ConvertTo = null,
        string? RatesVersion = null) : IQuery<CashFlowSeriesDto>, ICacheableQuery
    {
        public TimeSpan? CacheExpiration => ReportingCacheScopes.AggregatesExpiration;
        public string CacheScope => ReportingCacheScopes.Aggregates;
    }
}