using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Caching;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.Caching;
using Modules.Transactions.Application.Common.DTOs;
using SharedKernel.Common.ValueObjects;

namespace Modules.Transactions.Application.Features.Transactions.Queries.GetTransactionSummary
{
    public sealed record GetTransactionSummaryQuery(
        DateTime StartDate,
        DateTime EndDate,
        TransactionFilter Filter,
        string Currency = SupportedCurrency.Default) : IQuery<TransactionSummaryDto>, ICacheableQuery
    {
        public TimeSpan? CacheExpiration => TransactionCacheScopes.AggregatesExpiration;

        public string CacheScope => TransactionCacheScopes.Aggregates;
    }
}