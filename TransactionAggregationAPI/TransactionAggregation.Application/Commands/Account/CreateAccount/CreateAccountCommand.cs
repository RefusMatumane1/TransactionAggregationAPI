using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Commands.Account.CreateAccount
{
    public sealed record CreateAccountCommand(
        Guid CustomerId,
        string AccountNumber,
        string AccountName,
        AccountType AccountType,
        string Currency = "ZAR") : ICommand<Guid>;
}
