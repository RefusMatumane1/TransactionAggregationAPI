using Microsoft.Extensions.Hosting;
using Modules.Notifications.Application.Common;
using System.Net.Http.Json;

namespace Modules.Notifications.Infrastructure
{
    internal sealed class AlertWebhookPoster(IHttpClientFactory httpClientFactory) : IAlertWebhookPoster
    {
        public const string HttpClientName = "duplicate-alerts";

        public async Task PostAsync(string url, object payload, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
            OutboundUrlRedaction.Redact(request);

            using var response = await httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);

            // 4xx other than 408/429 is permanent; the rest is transient.
            response.EnsureSuccessStatusCode();
        }
    }
}