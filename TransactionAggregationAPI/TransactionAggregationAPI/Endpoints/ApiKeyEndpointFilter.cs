using Microsoft.EntityFrameworkCore;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Domain.Entities;

namespace TransactionAggregationAPI.Endpoints
{
    /// <summary>
    /// Verifies the X-Api-Key header on inbound webhook calls against the active WebhookSource
    /// rows in the database (managed live via WebhookSourceEndpoints/the admin UI — not static
    /// config) by an indexed hash lookup, so an admin's rotation or deactivation takes effect
    /// on the very next call with no caching/invalidation to get wrong.
    /// </summary>
    public sealed class ApiKeyEndpointFilter(IApplicationDbContext context) : IEndpointFilter
    {
        private const string HeaderName = "X-Api-Key";
        public const string SourceNameItemKey = "WebhookSourceName";

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context_, EndpointFilterDelegate next)
        {
            var providedKey = context_.HttpContext.Request.Headers[HeaderName].ToString();

            if (string.IsNullOrEmpty(providedKey))
                return Results.Unauthorized();

            var keyHash = WebhookSource.HashKey(providedKey);
            var cancellationToken = context_.HttpContext.RequestAborted;

            var source = await context.WebhookSources
                .FirstOrDefaultAsync(s => s.KeyHash == keyHash && s.IsActive, cancellationToken);

            if (source is null)
                return Results.Unauthorized();

            source.RecordUsage();
            await context.SaveChangesAsync(cancellationToken);

            context_.HttpContext.Items[SourceNameItemKey] = source.Name;

            return await next(context_);
        }
    }
}
