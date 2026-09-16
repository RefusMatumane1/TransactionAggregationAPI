using TransactionAggregation.Application.Abstractions;

namespace TransactionAggregation.Application.Features.WebhookSources.Commands.ActivateWebhookSource
{
    public sealed record ActivateWebhookSourceCommand(Guid Id) : ICommand;
}