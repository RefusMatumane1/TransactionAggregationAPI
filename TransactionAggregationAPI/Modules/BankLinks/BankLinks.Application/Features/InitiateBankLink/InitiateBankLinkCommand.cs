using Modules.BankLinks.Domain.ValueObjects;
using SharedKernel.Abstractions;

namespace Modules.BankLinks.Application.Features.InitiateBankLink
{
    public sealed record InitiateBankLinkCommand(Guid CustomerId, Institution Institution) : ICommand<string>;
}