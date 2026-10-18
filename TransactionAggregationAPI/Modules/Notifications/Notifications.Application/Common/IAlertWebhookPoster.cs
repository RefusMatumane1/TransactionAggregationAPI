namespace Modules.Notifications.Application.Common
{
    public interface IAlertWebhookPoster
    {
        Task PostAsync(string url, object payload, CancellationToken cancellationToken = default);
    }
}