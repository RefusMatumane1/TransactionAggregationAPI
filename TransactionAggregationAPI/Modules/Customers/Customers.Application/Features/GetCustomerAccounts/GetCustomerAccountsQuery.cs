using SharedKernel.Abstractions;
using Modules.Customers.Application.DTOs;

namespace Modules.Customers.Application.Features.GetCustomerAccounts
{
    public sealed record GetCustomerAccountsQuery(Guid CustomerId) : IQuery<IEnumerable<AccountDto>>;
}
