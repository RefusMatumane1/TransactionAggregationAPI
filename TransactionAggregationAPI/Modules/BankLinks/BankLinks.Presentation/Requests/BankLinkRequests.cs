using Modules.BankLinks.Application.Features.CompleteBankLink;
using Modules.BankLinks.Application.Features.InitiateBankLink;
using Modules.BankLinks.Domain.ValueObjects;

namespace Modules.BankLinks.Presentation.Requests
{
    public sealed record InitiateBankLinkRequest(Institution Institution)
    {
        internal InitiateBankLinkCommand ToCommand(Guid customerId) => new(customerId, Institution);
    }

    /// <summary>The OAuth redirect's query string.</summary>
    public sealed record CompleteBankLinkRequest(string Code, string State)
    {
        internal CompleteBankLinkCommand ToCommand() => new(Code, State);
    }
}