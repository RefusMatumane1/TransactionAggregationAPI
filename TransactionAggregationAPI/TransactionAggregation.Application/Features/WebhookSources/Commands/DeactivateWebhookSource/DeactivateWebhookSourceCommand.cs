using TransactionAggregation.Application.Abstractions;

namespace TransactionAggregation.Application.Features.WebhookSources.Commands.DeactivateWebhookSource
{
    public sealed record DeactivateWebhookSourceCommand(Guid Id) : ICommand;
}