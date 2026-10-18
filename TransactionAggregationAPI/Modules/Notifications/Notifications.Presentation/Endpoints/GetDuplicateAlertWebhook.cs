using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Notifications.Application.Features.GetDuplicateAlertWebhook;
using Modules.Notifications.Presentation.Responses;

namespace Modules.Notifications.Presentation.Endpoints
{
    internal static class GetDuplicateAlertWebhook
    {
        public static RouteHandlerBuilder MapGetDuplicateAlertWebhook(this IEndpointRouteBuilder group) =>
            group.MapGet("/{bankCode}", HandleAsync)
                 .WithName("GetDuplicateAlertWebhook")
                 .WithSummary("Whether this bank has its own duplicate-alert webhook (the URL itself is never returned)")
                 .Produces<DuplicateAlertWebhookResponse>()
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(ISender sender, string bankCode, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetDuplicateAlertWebhookQuery(bankCode), cancellationToken);
            return result.ToOk(DuplicateAlertWebhookResponse.From);
        }
    }
}