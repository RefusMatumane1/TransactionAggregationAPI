using BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Modules.WebhookSources.Presentation.Endpoints;

namespace Modules.WebhookSources
{
    public static class WebhookSourcesEndpoints
    {
        public static IEndpointRouteBuilder MapWebhookSourcesEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapApiGroup("admin/webhook-sources", "Admin")
                           .RequireAuthorization(AuthorizationPolicies.Admin);

            group.MapGetWebhookSources();
            group.MapCreateWebhookSource();
            group.MapRotateWebhookSourceKey();
            group.MapActivateWebhookSource();
            group.MapDeactivateWebhookSource();
            group.MapUpdateWebhookSourceInstitutions();

            return app;
        }
    }
}