using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Notifications.Presentation.Requests;

namespace Modules.Notifications.Presentation.Endpoints
{
    internal static class SetDuplicateAlertWebhook
    {
        public static RouteHandlerBuilder MapSetDuplicateAlertWebhook(this IEndpointRouteBuilder group) =>
            group.MapPut("/{bankCode}", HandleAsync)
                 .WithName("SetDuplicateAlertWebhook")
                 .WithSummary("Set (or, with a blank url, clear) where this bank's duplicate alerts are posted")
                 .Produces(StatusCodes.Status204NoContent)
                 .Produces(StatusCodes.Status400BadRequest)
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(
            ISender sender, string bankCode, [FromBody] SetDuplicateAlertWebhookRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(request.ToCommand(bankCode), cancellationToken);
            return result.ToNoContent();
        }
    }
}