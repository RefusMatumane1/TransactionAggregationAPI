using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel.Common.Auditing;

namespace BuildingBlocks.Web
{
    /// <summary>
    /// Audits a successful read of a resource addressed by a route value. One filter serves every module's endpoints;
    /// what is recorded, and where, is the <typeparamref name="TAudit"/> implementation's business.
    /// </summary>
    public sealed class ViewAuditFilter<TAudit>(TAudit audit) : IEndpointFilter
        where TAudit : IViewAudit
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var result = await next(context);

            var httpContext = context.HttpContext;
            if (result is not IStatusCodeHttpResult { StatusCode: >= 200 and < 300 }
                || !Guid.TryParse(httpContext.GetRouteValue(audit.RouteKey)?.ToString(), out var resourceId))
                return result;

            var view = httpContext.GetEndpoint()?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
                ?? httpContext.Request.Path.Value ?? "resource";

            // Not tied to the request: a client that disconnects after being served must not lose the audit row.
            await audit.RecordAsync(resourceId, view, CancellationToken.None);
            return result;
        }
    }
}
