using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.Attributes;

namespace TransactionAggregation.Application.Commands.BankLink.CompleteBankLink
{
    public sealed record CompleteBankLinkCommand(
    [property: Sensitive] string Code,
    [property: Sensitive] string State) : ICommand<Guid>;
}