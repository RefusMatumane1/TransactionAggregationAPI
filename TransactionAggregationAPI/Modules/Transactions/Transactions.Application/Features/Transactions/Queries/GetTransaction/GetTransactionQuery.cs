using Modules.Transactions.Application.Common.DTOs;
using SharedKernel.Abstractions;
using SharedKernel.Common.Behaviors;

namespace Modules.Transactions.Application.Features.Transactions.Queries.GetTransaction
{
    /// <summary>
    /// Scoped to <paramref name="CustomerId"/>: ownership is part of the lookup itself, so
    /// another customer's transaction and a missing one run the same query, match no row and
    /// fail at the same point (failures are never cached, and
    /// CustomerId is part of the cache key, so the owner's cached copy is never hit by anyone else) — neither the response nor its timing tells them apart.
    /// </summary>
    public sealed record GetTransactionQuery(Guid TransactionId, Guid CustomerId) : IQuery<TransactionDto>, ICacheableQuery
    {
        public TimeSpan? CacheExpiration => TimeSpan.FromMinutes(5);
    }
}