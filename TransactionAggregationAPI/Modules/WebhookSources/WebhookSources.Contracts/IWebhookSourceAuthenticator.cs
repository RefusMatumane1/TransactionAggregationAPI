namespace Modules.WebhookSources.Contracts
{
    public interface IWebhookSourceAuthenticator
    {
        Task<string?> AuthenticateAsync(string apiKey, CancellationToken cancellationToken = default);
    }
}