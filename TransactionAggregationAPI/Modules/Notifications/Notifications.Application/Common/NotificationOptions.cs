namespace Modules.Notifications.Application.Common
{
    public sealed class NotificationOptions
    {
        public const string SectionName = "NotificationOptions";

        // Default destination for banks that have no alert URL of their own. Optional; a URL embeds a credential.
        public string DuplicateAlertWebhookUrl { get; set; } = string.Empty;
    }
}