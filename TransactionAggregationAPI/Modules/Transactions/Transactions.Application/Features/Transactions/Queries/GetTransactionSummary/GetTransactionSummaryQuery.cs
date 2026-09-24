using Modules.Transactions.Application.Common.DTOs;
using SharedKernel.Abstractions;
using SharedKernel.Common.Behaviors;

namespace Modules.Transactions.Application.Features.Transactions.Queries.GetTransactionSummary
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