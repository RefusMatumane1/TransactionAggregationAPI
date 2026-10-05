using BuildingBlocks.Application.Abstractions;

namespace Modules.WebhookSources.Application.Features.CreateWebhookSource
{
    public sealed record CreateWebhookSourceCommand(string Code, string DisplayName, string Color) : ICommand<CreateWebhookSourceResult>;

    public sealed record CreateWebhookSourceResult(Guid Id, string Code, string DisplayName, string Color, string ApiKey);
}