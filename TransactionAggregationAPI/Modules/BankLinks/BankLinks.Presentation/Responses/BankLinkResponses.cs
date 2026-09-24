using Modules.BankLinks.Application.DTOs;
using Modules.BankLinks.Domain.ValueObjects;

namespace Modules.BankLinks.Presentation.Responses
{
    public sealed record BankLinkResponse(
        Guid Id,
        Institution Institution,
        BankLinkStatus Status,
        Guid? AccountId,
        DateTime LinkedAt)
    {
        internal static BankLinkResponse From(BankLinkDto link) =>
            new(link.Id, link.Institution, link.Status, link.AccountId, link.LinkedAt);
    }

    public sealed record InitiateBankLinkResponse(string AuthorizationUrl);

    public sealed record CompleteBankLinkResponse(Guid AccountId);
}