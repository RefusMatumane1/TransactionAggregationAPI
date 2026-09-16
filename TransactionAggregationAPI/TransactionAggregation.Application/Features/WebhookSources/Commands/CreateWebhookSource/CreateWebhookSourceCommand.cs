using TransactionAggregation.Application.Abstractions;

namespace TransactionAggregation.Application.Features.WebhookSources.Commands.CreateWebhookSource
{
    public sealed record CreateWebhookSourceCommand(string Name) : ICommand<CreateWebhookSourceResult>;

    /// <summary>ApiKey is the plaintext key — this is the only response that will ever contain
    /// it. It is not recoverable afterwards, only rotatable (see RotateWebhookSourceKeyCommand).</summary>
    public sealed record CreateWebhookSourceResult(Guid Id, string Name, string ApiKey);
}
