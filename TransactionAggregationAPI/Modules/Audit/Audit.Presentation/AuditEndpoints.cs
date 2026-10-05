using BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Modules.Audit.Presentation.Endpoints;

namespace Modules.Audit
{
    public static class AuditEndpoints
    {
        public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapApiGroup("admin/audit", "Admin")
                           .RequireAuthorization(AuthorizationPolicies.Admin);

            group.MapSearchAuditEvents();
            group.MapGetTransactionLineage();

            return app;
        }
    }
}