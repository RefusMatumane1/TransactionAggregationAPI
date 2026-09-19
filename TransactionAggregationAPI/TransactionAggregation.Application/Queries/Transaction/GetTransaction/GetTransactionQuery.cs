using SharedKernel.Abstractions;
using SharedKernel.Common.Behaviors;
using TransactionAggregation.Application.Common.DTOs;

namespace TransactionAggregation.Application.Queries.Transaction.GetTransaction
{
    public sealed record GetTransactionQuery(Guid TransactionId) : IQuery<TransactionDto>, ICacheableQuery
    {
        public TimeSpan? CacheExpiration => TimeSpan.FromMinutes(5);
    }
}