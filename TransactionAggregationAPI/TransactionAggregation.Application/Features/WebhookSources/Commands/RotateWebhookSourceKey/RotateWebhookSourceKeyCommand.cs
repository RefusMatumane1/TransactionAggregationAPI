using TransactionAggregation.Application.Abstractions;

namespace TransactionAggregation.Application.Features.WebhookSources.Commands.RotateWebhookSourceKey
{
    /// <summary>Result is the new plaintext key — shown exactly once, same as at creation. The
    /// old key stops working the instant this is saved (its hash is overwritten, not retained).</summary>
    public sealed record RotateWebhookSourceKeyCommand(Guid Id) : ICommand<string>;
}
