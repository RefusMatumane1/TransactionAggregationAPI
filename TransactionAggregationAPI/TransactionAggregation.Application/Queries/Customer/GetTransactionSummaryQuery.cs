using SharedKernel.Abstractions;
using SharedKernel.Common.Behaviors;
using TransactionAggregation.Application.Common.DTOs;

namespace TransactionAggregation.Application.Queries.Customer
{
    public sealed record GetTransactionSummaryQuery(
        Guid CustomerId,
        DateTime StartDate,
        DateTime EndDate) : IQuery<TransactionSummaryDto>, ICacheableQuery, ICacheKeyPrefix
    {
        public TimeSpan? CacheExpiration => TimeSpan.FromMinutes(5);

        public string CachePrefix => $"summary:{CustomerId}";
    }
}