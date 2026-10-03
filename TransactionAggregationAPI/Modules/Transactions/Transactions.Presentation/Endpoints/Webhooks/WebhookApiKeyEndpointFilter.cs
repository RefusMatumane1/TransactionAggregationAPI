using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Modules.Audit.Contracts;
using Modules.WebhookSources.Contracts;

namespace Modules.Transactions.Presentation.Endpoints.Webhooks
{
    internal sealed class WebhookApiKeyEndpointFilter(
        IWebhookSourceAuthenticator authenticator,
        IAuditTrail auditTrail,
        ILogger<WebhookApiKeyEndpointFilter> logger) : IEndpointFilter
    {
        private const string HeaderName = "X-Api-Key";
        private const string SourceNameItemKey = "WebhookSourceName";

        public static string GetSourceName(HttpContext httpContext) =>
            (string)httpContext.Items[SourceNameItemKey]!;

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var httpContext = context.HttpContext;
            var providedKey = httpContext.Request.Headers[HeaderName].ToString();

            if (string.IsNullOrEmpty(providedKey))
                return await RejectAsync(httpContext, $"Missing {HeaderName} header");

            var sourceName = await authenticator.AuthenticateAsync(providedKey, httpContext.RequestAborted);

            if (sourceName is null)
                return await RejectAsync(httpContext, "Unknown or inactive API key");

            httpContext.Items[SourceNameItemKey] = sourceName;

            return await next(context);
        }

        private async Task<IResult> RejectAsync(HttpContext httpContext, string reason)
        {
            await WebhookDeliveryAudit.TryRecordAsync(
                auditTrail, logger, httpContext,
                AuditEventTypes.InboundUnauthorized,
                WebhookDeliveryAudit.UnauthenticatedSource,
                reason);

            return Results.Unauthorized();
        }
    }
}