using Modules.Transactions.Application.Common.DTOs;
using SharedKernel.Abstractions;
using SharedKernel.Common.Behaviors;

namespace Modules.Transactions.Application.Features.Transactions.Queries.GetTransaction
{
    /// <summary>
    /// Scoped to <paramref name="CustomerId"/>: another customer's transaction and a missing one run
    /// the same query and fail the same way, so neither the response nor its timing tells them apart.
    /// Failures are never cached, and CustomerId is part of the cache key.
    /// </summary>
    public sealed record GetTransactionQuery(Guid TransactionId, Guid CustomerId) : IQuery<TransactionDto>, ICacheableQuery
    {
        public TimeSpan? CacheExpiration => TimeSpan.FromMinutes(5);
    }
}