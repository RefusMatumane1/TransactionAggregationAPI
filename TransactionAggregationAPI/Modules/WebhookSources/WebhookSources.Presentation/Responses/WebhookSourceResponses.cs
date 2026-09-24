using Modules.WebhookSources.Application.DTOs;
using Modules.WebhookSources.Application.Features.CreateWebhookSource;

namespace Modules.WebhookSources.Presentation.Responses
{
    /// <summary>A webhook source as listed to admins — deliberately carries no key material.</summary>
    public sealed record WebhookSourceResponse(
        Guid Id, string Name, bool IsActive, DateTime CreatedAt, DateTime? LastUsedAt, IReadOnlyList<string> AuthorizedInstitutions)
    {
        internal static WebhookSourceResponse From(WebhookSourceDto source) =>
            new(source.Id, source.Name, source.IsActive, source.CreatedAt, source.LastUsedAt, source.AuthorizedInstitutions);
    }

    /// <summary>The only response that ever carries a new source's plaintext API key.</summary>
    public sealed record CreateWebhookSourceResponse(Guid Id, string Name, string ApiKey, IReadOnlyList<string> AuthorizedInstitutions)
    {
        internal static CreateWebhookSourceResponse From(CreateWebhookSourceResult result) =>
            new(result.Id, result.Name, result.ApiKey, result.AuthorizedInstitutions);
    }

    public sealed record RotateWebhookSourceKeyResponse(string ApiKey);
}