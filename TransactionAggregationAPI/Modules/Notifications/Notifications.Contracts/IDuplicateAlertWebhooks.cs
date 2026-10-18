namespace Modules.Notifications.Contracts
{
    public enum AlertWebhookOutcome
    {
        Set,
        InvalidUrl,
        UnknownBank
    }

    public interface IDuplicateAlertWebhooks
    {
        bool IsValidUrl(string? url);

        Task<AlertWebhookOutcome> SetAsync(string bankCode, string url, CancellationToken cancellationToken = default);
    }
}