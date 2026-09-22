using SharedKernel.Abstractions;
using Modules.BankLinks.ValueObjects;

namespace Modules.BankLinks.Features.InitiateBankLink
{
    public sealed record InitiateBankLinkCommand(Guid CustomerId, Institution Institution) : ICommand<string>;
}
