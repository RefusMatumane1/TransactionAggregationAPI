using SharedKernel.Abstractions;

namespace Modules.WebhookSources.Application.Features.DeactivateWebhookSource
{
    public sealed record DeactivateWebhookSourceCommand(Guid Id) : ICommand;
}