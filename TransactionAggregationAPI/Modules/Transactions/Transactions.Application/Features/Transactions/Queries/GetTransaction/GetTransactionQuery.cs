using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Application.Caching;
using Modules.Transactions.Application.Common.Caching;
using Modules.Transactions.Application.Common.DTOs;

namespace Modules.Transactions.Application.Features.Transactions.Queries.GetTransaction
{
    public sealed record GetTransactionQuery(Guid TransactionId, InstitutionAccess Access) : IQuery<TransactionDto>, ICacheableQuery
    {
        public TimeSpan? CacheExpiration => TimeSpan.FromMinutes(5);

        public string CacheScope => TransactionCacheScopes.All;
    }
}