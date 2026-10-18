using BuildingBlocks.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Modules.Audit.Contracts;

namespace Modules.Customers.Presentation.Endpoints
{
    // A customer's data leaves only after the view is recorded: if the audit write fails, so does the read.
    internal sealed class CustomerViewAuditFilter(IAuditTrail auditTrail, IUserContext user) : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var result = await next(context);

            var httpContext = context.HttpContext;
            if (result is not IStatusCodeHttpResult { StatusCode: >= 200 and < 300 }
                || !Guid.TryParse(httpContext.GetRouteValue("customerId")?.ToString(), out var customerId))
                return result;

            var view = httpContext.GetEndpoint()?.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName
                ?? httpContext.Request.Path.Value ?? "customer";
            var access = user.InstitutionAccess;
            var scope = access.AllInstitutions ? "all" : string.Join(',', access.Institutions);

            await auditTrail.RecordAsync([AccessAudit.CustomerViewed(user.UserId, customerId, view, scope)], CancellationToken.None);
            return result;
        }
    }
}
