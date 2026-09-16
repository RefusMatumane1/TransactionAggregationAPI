using TransactionAggregation.Application.Abstractions;

namespace TransactionAggregation.Application.Features.WebhookSources.Commands.RotateWebhookSourceKey
{
    public sealed record RotateWebhookSourceKeyCommand(Guid Id) : ICommand<string>;
}