using TransactionAggregation.Application.Abstractions;

namespace TransactionAggregation.Application.Commands.Account.DeactivateAccount
{
    public sealed record DeactivateAccountCommand(Guid AccountId) : ICommand;
}
