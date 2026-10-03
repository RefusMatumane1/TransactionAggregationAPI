using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.WebhookSources.Presentation.Requests;

namespace Modules.WebhookSources.Presentation.Endpoints
{
    internal static class UpdateWebhookSource
    {
        public static RouteHandlerBuilder MapUpdateWebhookSource(this IEndpointRouteBuilder group) =>
            group.MapPut("/{id:guid}", HandleAsync)
                 .WithName("UpdateWebhookSource")
                 .WithSummary("Change how a bank is shown: its display name and colour (the code never changes)")
                 .Produces(StatusCodes.Status204NoContent)
                 .ProducesValidationProblem()
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(
            ISender sender, Guid id, [FromBody] UpdateWebhookSourceRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(request.ToCommand(id), cancellationToken);

            return result.ToNoContent();
        }
    }
}