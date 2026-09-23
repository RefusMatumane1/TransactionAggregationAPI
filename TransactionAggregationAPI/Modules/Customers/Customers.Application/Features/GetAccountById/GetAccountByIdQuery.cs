using SharedKernel.Abstractions;
using Modules.Customers.Application.DTOs;

namespace Modules.Customers.Application.Features.GetAccountById
{
    public sealed record GetAccountByIdQuery(Guid AccountId) : IQuery<AccountDto>;
}
