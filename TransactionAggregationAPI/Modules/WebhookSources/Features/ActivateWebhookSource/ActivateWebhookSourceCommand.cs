using SharedKernel.Abstractions;

namespace Modules.WebhookSources.Features.ActivateWebhookSource
{
    public sealed record ActivateWebhookSourceCommand(Guid Id) : ICommand;
}
