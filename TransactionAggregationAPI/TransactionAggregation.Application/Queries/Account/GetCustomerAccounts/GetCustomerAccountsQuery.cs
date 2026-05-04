using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;

namespace TransactionAggregation.Application.Queries.Account.GetCustomerAccounts
{
    public sealed record GetCustomerAccountsQuery(Guid CustomerId) : IQuery<IEnumerable<AccountDto>>;
}
