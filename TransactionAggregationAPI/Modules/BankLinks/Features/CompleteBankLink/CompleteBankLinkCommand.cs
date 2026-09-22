using SharedKernel.Abstractions;
using SharedKernel.Common.Attributes;

namespace Modules.BankLinks.Features.CompleteBankLink
{
    public sealed record CompleteBankLinkCommand(
        [property: Sensitive] string Code,
        [property: Sensitive] string State) : ICommand<Guid>;
}
