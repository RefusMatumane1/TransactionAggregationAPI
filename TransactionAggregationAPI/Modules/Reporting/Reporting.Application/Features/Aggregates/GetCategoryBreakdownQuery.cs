using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Caching;
using BuildingBlocks.Application.Querying;
using Modules.Reporting.Application.Aggregation;
using Modules.Reporting.Application.DTOs;

namespace Modules.Reporting.Application.Features.Aggregates
{
    public sealed record GetCategoryBreakdownQuery(
        DateOnly From,
        DateOnly To,
        AccountFilter Filter,
        CashFlowDirection Direction = CashFlowDirection.Expense,
        string? Currency = null,
        string? ConvertTo = null,
        string? RatesVersion = null) : IQuery<CategoryBreakdownDto>, ICacheableQuery
    {
        public TimeSpan? CacheExpiration => ReportingCacheScopes.AggregatesExpiration;
        public string CacheScope => ReportingCacheScopes.Aggregates;
    }
}