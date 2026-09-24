using Modules.Customers.Domain.ValueObjects;
using SharedKernel.Abstractions;

namespace Modules.Customers.Application.Features.CreateAccount
{
    public sealed record CreateAccountCommand(
        Guid CustomerId,
        string AccountNumber,
        string AccountName,
        AccountType AccountType,
        string Currency = "ZAR") : ICommand<Guid>;
}