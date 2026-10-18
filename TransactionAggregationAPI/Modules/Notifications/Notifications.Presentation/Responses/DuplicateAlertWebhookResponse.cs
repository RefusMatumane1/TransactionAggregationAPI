using Modules.Notifications.Application.DTOs;

namespace Modules.Notifications.Presentation.Responses
{
    public sealed record DuplicateAlertWebhookResponse(string BankCode, bool Configured, DateTime? UpdatedAt)
    {
        internal static DuplicateAlertWebhookResponse From(DuplicateAlertWebhookDto dto) =>
            new(dto.BankCode, dto.Configured, dto.UpdatedAt);
    }
}