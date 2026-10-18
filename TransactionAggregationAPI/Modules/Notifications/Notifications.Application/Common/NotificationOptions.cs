namespace Modules.Notifications.Application.Common
{
    public sealed class NotificationOptions
    {
        public const string SectionName = "NotificationOptions";

        public string DuplicateAlertWebhookUrl { get; set; } = string.Empty;
    }
}