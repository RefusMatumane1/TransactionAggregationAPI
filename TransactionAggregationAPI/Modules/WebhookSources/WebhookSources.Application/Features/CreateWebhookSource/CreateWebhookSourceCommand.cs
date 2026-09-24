using SharedKernel.Abstractions;

namespace Modules.WebhookSources.Application.Features.CreateWebhookSource
{
    public sealed record CreateWebhookSourceCommand(string Name, IReadOnlyList<string> AuthorizedInstitutions) : ICommand<CreateWebhookSourceResult>;

    public sealed record CreateWebhookSourceResult(Guid Id, string Name, string ApiKey, IReadOnlyList<string> AuthorizedInstitutions);
}