namespace Modules.WebhookSources.Contracts
{
    /// <summary>
    /// Published contract for authenticating inbound webhook calls by API key. The
    /// Transactions module's webhook endpoint uses it to find out which source is calling
    /// without depending on WebhookSources' persistence or domain types.
    /// </summary>
    public interface IWebhookSourceAuthenticator
    {
        /// <summary>
        /// Returns the name of the active source that owns <paramref name="apiKey"/> and
        /// records the usage, or null when the key is unknown or its source is inactive.
        /// </summary>
        Task<string?> AuthenticateAsync(string apiKey, CancellationToken cancellationToken = default);
    }
}
