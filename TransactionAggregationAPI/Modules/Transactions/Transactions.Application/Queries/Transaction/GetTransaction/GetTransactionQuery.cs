using SharedKernel.Abstractions;
using SharedKernel.Common.Behaviors;
using Modules.Transactions.Application.Common.DTOs;

namespace Modules.Transactions.Application.Queries.Transaction.GetTransaction
{
    public sealed record GetTransactionQuery(Guid TransactionId) : IQuery<TransactionDto>, ICacheableQuery
    {
        public TimeSpan? CacheExpiration => TimeSpan.FromMinutes(5);
    }
}