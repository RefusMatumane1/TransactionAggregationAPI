using Modules.WebhookSources.Application.Features.CreateWebhookSource;
using Modules.WebhookSources.Application.Features.UpdateWebhookSourceInstitutions;

namespace Modules.WebhookSources.Presentation.Requests
{
    /// <param name="AuthorizedInstitutions">The institutions this source may deliver transactions for — required, at least one.</param>
    public sealed record CreateWebhookSourceRequest(string Name, IReadOnlyList<string>? AuthorizedInstitutions)
    {
        internal CreateWebhookSourceCommand ToCommand() => new(Name, AuthorizedInstitutions ?? []);
    }

    public sealed record UpdateWebhookSourceInstitutionsRequest(IReadOnlyList<string>? AuthorizedInstitutions)
    {
        internal UpdateWebhookSourceInstitutionsCommand ToCommand(Guid id) => new(id, AuthorizedInstitutions ?? []);
    }
}