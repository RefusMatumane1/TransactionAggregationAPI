namespace Modules.Notifications.Application.DTOs
{
    // Never carries the URL: it embeds a credential and is write-only.
    public sealed record DuplicateAlertWebhookDto(string BankCode, bool Configured, DateTime? UpdatedAt);
}