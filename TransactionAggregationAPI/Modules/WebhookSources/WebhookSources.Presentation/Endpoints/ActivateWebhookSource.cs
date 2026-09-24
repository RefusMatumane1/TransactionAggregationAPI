using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.WebhookSources.Application.Features.ActivateWebhookSource;

namespace Modules.WebhookSources.Presentation.Endpoints
{
    internal static class ActivateWebhookSource
    {
        public static RouteHandlerBuilder MapActivateWebhookSource(this IEndpointRouteBuilder group) =>
            group.MapPost("/{id:guid}/activate", HandleAsync)
                 .WithName("ActivateWebhookSource")
                 .Produces(StatusCodes.Status204NoContent)
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(ISender sender, Guid id, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new ActivateWebhookSourceCommand(id), cancellationToken);

            return result.ToNoContent();
        }
    }
}