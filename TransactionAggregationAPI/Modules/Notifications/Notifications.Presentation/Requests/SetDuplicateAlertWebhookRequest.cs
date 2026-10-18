using Modules.Notifications.Application.Features.SetDuplicateAlertWebhook;

namespace Modules.Notifications.Presentation.Requests
{
    public sealed record SetDuplicateAlertWebhookRequest(string? Url)
    {
        internal SetDuplicateAlertWebhookCommand ToCommand(string bankCode) => new(bankCode, Url);
    }
}