using SharedKernel.Abstractions;

namespace Modules.WebhookSources.Features.CreateWebhookSource
{
    public sealed record CreateWebhookSourceCommand(string Name) : ICommand<CreateWebhookSourceResult>;

    public sealed record CreateWebhookSourceResult(Guid Id, string Name, string ApiKey);
}
