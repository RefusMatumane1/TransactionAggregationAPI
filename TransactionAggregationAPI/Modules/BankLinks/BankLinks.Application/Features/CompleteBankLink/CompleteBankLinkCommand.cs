using SharedKernel.Abstractions;
using SharedKernel.Common.Attributes;

namespace Modules.BankLinks.Application.Features.CompleteBankLink
{
    public sealed record CompleteBankLinkCommand(
        [property: Sensitive] string Code,
        [property: Sensitive] string State) : ICommand<Guid>;
}
