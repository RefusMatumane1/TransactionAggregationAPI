using Modules.Notifications.Application.DTOs;

namespace Modules.Notifications.Presentation.Responses
{
    // The URL is write-only: it embeds a credential, so a read says only whether one is set.
    public sealed record DuplicateAlertWebhookResponse(string BankCode, bool Configured, DateTime? UpdatedAt)
    {
        internal static DuplicateAlertWebhookResponse From(DuplicateAlertWebhookDto dto) =>
            new(dto.BankCode, dto.Configured, dto.UpdatedAt);
    }
}