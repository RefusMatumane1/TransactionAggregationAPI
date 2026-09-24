using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.WebhookSources.Application.Features.DeactivateWebhookSource;

namespace Modules.WebhookSources.Presentation.Endpoints
{
    internal static class DeactivateWebhookSource
    {
        public static RouteHandlerBuilder MapDeactivateWebhookSource(this IEndpointRouteBuilder group) =>
            group.MapPost("/{id:guid}/deactivate", HandleAsync)
                 .WithName("DeactivateWebhookSource")
                 .Produces(StatusCodes.Status204NoContent)
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(ISender sender, Guid id, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new DeactivateWebhookSourceCommand(id), cancellationToken);

            return result.ToNoContent();
        }
    }
}