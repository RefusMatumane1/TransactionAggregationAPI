namespace Modules.Notifications.Application.Common
{
    // Port to the outside world. A 4xx other than 408/429 is permanent, anything else that fails is transient:
    // implementations throw HttpRequestException carrying the status so the caller's failure classifier can tell.
    public interface IAlertWebhookPoster
    {
        Task PostAsync(string url, object payload, CancellationToken cancellationToken = default);
    }
}