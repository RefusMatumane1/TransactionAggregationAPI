using SharedKernel.Abstractions;

namespace Modules.WebhookSources.Application.Features.ActivateWebhookSource
{
    public sealed record ActivateWebhookSourceCommand(Guid Id) : ICommand;
}
