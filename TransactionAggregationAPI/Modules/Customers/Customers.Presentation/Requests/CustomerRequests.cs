using Modules.Customers.Application.Features.CreateAccount;
using Modules.Customers.Application.Features.CreateCustomer;
using Modules.Customers.Application.Features.UpdateCustomer;
using Modules.Customers.Domain.ValueObjects;

namespace Modules.Customers.Presentation.Requests
{
    public sealed record CreateCustomerRequest(string Email, string Name, string Password)
    {
        internal CreateCustomerCommand ToCommand() => new(Email, Name, Password);
    }

    public sealed record UpdateCustomerRequest(string Email, string Name)
    {
        internal UpdateCustomerCommand ToCommand(Guid customerId) => new(customerId, Email, Name);
    }

    public sealed record CreateAccountRequest(
        string AccountNumber,
        string AccountName,
        AccountType AccountType,
        string Currency = "ZAR")
    {
        internal CreateAccountCommand ToCommand(Guid customerId) =>
            new(customerId, AccountNumber, AccountName, AccountType, Currency);
    }
}