using Microsoft.AspNetCore.Http;
using Modules.WebhookSources.Contracts;

namespace Modules.Transactions.Infrastructure.Endpoints
{
    /// <summary>
    /// Authenticates inbound bank-aggregator webhooks by the X-Api-Key header. Key lookup
    /// and usage tracking belong to WebhookSources, reached only through its published
    /// IWebhookSourceAuthenticator contract.
    /// </summary>
    internal sealed class WebhookApiKeyEndpointFilter(IWebhookSourceAuthenticator authenticator) : IEndpointFilter
    {
        private const string HeaderName = "X-Api-Key";
        public const string SourceNameItemKey = "WebhookSourceName";

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var providedKey = context.HttpContext.Request.Headers[HeaderName].ToString();

            if (string.IsNullOrEmpty(providedKey))
                return Results.Unauthorized();

            var sourceName = await authenticator.AuthenticateAsync(providedKey, context.HttpContext.RequestAborted);

            if (sourceName is null)
                return Results.Unauthorized();

            context.HttpContext.Items[SourceNameItemKey] = sourceName;

            return await next(context);
        }
    }
}
