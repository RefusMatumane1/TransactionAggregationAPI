using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.WebhookSources.Application.Features.RotateWebhookSourceKey;
using Modules.WebhookSources.Presentation.Responses;

namespace Modules.WebhookSources.Presentation.Endpoints
{
    internal static class RotateWebhookSourceKey
    {
        public static RouteHandlerBuilder MapRotateWebhookSourceKey(this IEndpointRouteBuilder group) =>
            group.MapPost("/{id:guid}/rotate", HandleAsync)
                 .WithName("RotateWebhookSourceKey")
                 .WithSummary("Replace a source's key — the old one stops working immediately; the new one is shown exactly once")
                 .Produces<RotateWebhookSourceKeyResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(ISender sender, Guid id, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new RotateWebhookSourceKeyCommand(id), cancellationToken);

            return result.ToOk(apiKey => new RotateWebhookSourceKeyResponse(apiKey));
        }
    }
}