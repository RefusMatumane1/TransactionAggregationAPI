using Modules.BankLinks.Domain.ValueObjects;

namespace Modules.BankLinks.Infrastructure.Endpoints
{
    public sealed record InitiateBankLinkRequest(Institution Institution);

    public sealed record InitiateBankLinkResponse(string AuthorizationUrl);
    public sealed record CompleteBankLinkResponse(Guid AccountId);
}
