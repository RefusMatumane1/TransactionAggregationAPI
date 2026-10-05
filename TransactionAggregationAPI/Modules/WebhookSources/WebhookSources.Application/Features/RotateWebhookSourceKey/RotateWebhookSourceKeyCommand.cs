using BuildingBlocks.Application.Abstractions;

namespace Modules.WebhookSources.Application.Features.RotateWebhookSourceKey
{
    public sealed record RotateWebhookSourceKeyCommand(Guid Id) : ICommand<string>;
}