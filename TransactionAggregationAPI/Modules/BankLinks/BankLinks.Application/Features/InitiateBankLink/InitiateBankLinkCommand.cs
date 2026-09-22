using SharedKernel.Abstractions;
using Modules.BankLinks.Domain.ValueObjects;

namespace Modules.BankLinks.Application.Features.InitiateBankLink
{
    public sealed record InitiateBankLinkCommand(Guid CustomerId, Institution Institution) : ICommand<string>;
}
