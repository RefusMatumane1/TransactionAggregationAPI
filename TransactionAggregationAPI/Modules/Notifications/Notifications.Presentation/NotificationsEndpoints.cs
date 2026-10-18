using BuildingBlocks.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Modules.Notifications.Presentation.Endpoints;

namespace Modules.Notifications
{
    public static class NotificationsEndpoints
    {
        public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapApiGroup("admin/notifications/duplicate-alert-webhooks", "Notifications")
                           .RequireAuthorization(AuthorizationPolicies.Admin);

            group.MapGetDuplicateAlertWebhook();
            group.MapSetDuplicateAlertWebhook();

            return app;
        }
    }
}