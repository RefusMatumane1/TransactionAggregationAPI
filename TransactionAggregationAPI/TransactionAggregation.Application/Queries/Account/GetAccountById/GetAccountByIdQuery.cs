using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;

namespace TransactionAggregation.Application.Queries.Account.GetAccountById
{
    public sealed record GetAccountByIdQuery(Guid AccountId) : IQuery<AccountDto>;
}
