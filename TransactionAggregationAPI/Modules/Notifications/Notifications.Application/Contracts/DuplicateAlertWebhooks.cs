using MediatR;
using Modules.Notifications.Application.Features.SetDuplicateAlertWebhook;
using Modules.Notifications.Contracts;
using Modules.Notifications.Domain;
using SharedKernel.Common.Enums;

namespace Modules.Notifications.Application.Contracts
{
    internal sealed class DuplicateAlertWebhooks(ISender sender) : IDuplicateAlertWebhooks
    {
        public bool IsValidUrl(string? url) => AlertWebhook.IsValidUrl(url);

        public async Task<AlertWebhookOutcome> SetAsync(string bankCode, string url, CancellationToken cancellationToken = default)
        {
            var result = await sender.Send(new SetDuplicateAlertWebhookCommand(bankCode, url), cancellationToken);

            return result.IsSuccess
                ? AlertWebhookOutcome.Set
                : result.Error.Type switch
                {
                    ErrorType.NotFound => AlertWebhookOutcome.UnknownBank,
                    ErrorType.Validation => AlertWebhookOutcome.InvalidUrl,
                    _ => throw new InvalidOperationException(result.Error.Description)
                };
        }
    }
}