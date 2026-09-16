using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Application.Commands.BankLink.InitiateBankLink
{
    public sealed record InitiateBankLinkCommand(Guid CustomerId, Institution Institution) : ICommand<string>;
}