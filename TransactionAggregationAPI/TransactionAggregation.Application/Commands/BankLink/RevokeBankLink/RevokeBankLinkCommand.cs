using TransactionAggregation.Application.Abstractions;

namespace TransactionAggregation.Application.Commands.BankLink.RevokeBankLink
{
    public sealed record RevokeBankLinkCommand(Guid CustomerId, Guid BankLinkId) : ICommand;
}
