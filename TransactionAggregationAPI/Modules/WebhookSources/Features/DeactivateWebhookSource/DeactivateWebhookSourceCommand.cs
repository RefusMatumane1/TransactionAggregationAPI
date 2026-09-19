using SharedKernel.Abstractions;

namespace Modules.WebhookSources.Features.DeactivateWebhookSource
{
    public sealed record DeactivateWebhookSourceCommand(Guid Id) : ICommand;
}
