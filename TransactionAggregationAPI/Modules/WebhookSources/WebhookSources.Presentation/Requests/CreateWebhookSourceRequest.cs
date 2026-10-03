using Modules.WebhookSources.Application.Features.CreateWebhookSource;
using Modules.WebhookSources.Application.Features.RegisterWebhookSourceSigningKey;
using Modules.WebhookSources.Application.Features.UpdateWebhookSource;
using Modules.WebhookSources.Domain;

namespace Modules.WebhookSources.Presentation.Requests
{
    public sealed record CreateWebhookSourceRequest(string? Code, string? DisplayName, string? Color)
    {
        internal CreateWebhookSourceCommand ToCommand() =>
            new(Code ?? string.Empty, DisplayName ?? string.Empty, Color ?? WebhookSource.DefaultColor);
    }

    public sealed record UpdateWebhookSourceRequest(string? DisplayName, string? Color)
    {
        internal UpdateWebhookSourceCommand ToCommand(Guid id) => new(id, DisplayName ?? string.Empty, Color ?? string.Empty);
    }

    public sealed record RegisterWebhookSourceSigningKeyRequest(string? PublicKey)
    {
        internal RegisterWebhookSourceSigningKeyCommand ToCommand(Guid id) => new(id, PublicKey ?? string.Empty);
    }
}