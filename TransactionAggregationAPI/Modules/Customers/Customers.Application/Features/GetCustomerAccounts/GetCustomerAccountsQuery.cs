using Modules.Customers.Application.DTOs;
using SharedKernel.Abstractions;

namespace Modules.Customers.Application.Features.GetCustomerAccounts
{
    public sealed record GetCustomerAccountsQuery(Guid CustomerId) : IQuery<IEnumerable<AccountDto>>;
}