using SharedKernel.Abstractions;

namespace Modules.WebhookSources.Features.RotateWebhookSourceKey
{
    public sealed record RotateWebhookSourceKeyCommand(Guid Id) : ICommand<string>;
}
