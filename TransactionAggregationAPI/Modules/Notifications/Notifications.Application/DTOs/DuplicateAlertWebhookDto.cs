namespace Modules.Notifications.Application.DTOs
{
    public sealed record DuplicateAlertWebhookDto(string BankCode, bool Configured, DateTime? UpdatedAt);
}