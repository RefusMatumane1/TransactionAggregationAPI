using SharedKernel.Abstractions;
using SharedKernel.Common.Attributes;

namespace TransactionAggregation.Application.Commands.BankLink.CompleteBankLink
{
    public sealed record CompleteBankLinkCommand(
    [property: Sensitive] string Code,
    [property: Sensitive] string State) : ICommand<Guid>;
}