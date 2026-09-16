using TransactionAggregation.Application.Abstractions;

namespace TransactionAggregation.Application.Features.WebhookSources.Commands.CreateWebhookSource
{
    public sealed record CreateWebhookSourceCommand(string Name) : ICommand<CreateWebhookSourceResult>;

    public sealed record CreateWebhookSourceResult(Guid Id, string Name, string ApiKey);
}