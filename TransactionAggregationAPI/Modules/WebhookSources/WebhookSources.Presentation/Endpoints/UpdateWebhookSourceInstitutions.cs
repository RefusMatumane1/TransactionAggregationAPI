using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.WebhookSources.Presentation.Requests;

namespace Modules.WebhookSources.Presentation.Endpoints
{
    internal static class UpdateWebhookSourceInstitutions
    {
        public static RouteHandlerBuilder MapUpdateWebhookSourceInstitutions(this IEndpointRouteBuilder group) =>
            group.MapPut("/{id:guid}/institutions", HandleAsync)
                 .WithName("UpdateWebhookSourceInstitutions")
                 .WithSummary("Replace the institutions a webhook source may deliver transactions for")
                 .Produces(StatusCodes.Status204NoContent)
                 .Produces(StatusCodes.Status400BadRequest)
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(
            ISender sender, Guid id, [FromBody] UpdateWebhookSourceInstitutionsRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(request.ToCommand(id), cancellationToken);

            return result.ToNoContent();
        }
    }
}