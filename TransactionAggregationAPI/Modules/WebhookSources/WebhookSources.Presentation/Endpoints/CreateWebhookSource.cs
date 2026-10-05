using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.WebhookSources.Presentation.Requests;
using Modules.WebhookSources.Presentation.Responses;

namespace Modules.WebhookSources.Presentation.Endpoints
{
    internal static class CreateWebhookSource
    {
        public static RouteHandlerBuilder MapCreateWebhookSource(this IEndpointRouteBuilder group) =>
            group.MapPost("/", HandleAsync)
                 .WithName("CreateWebhookSource")
                 .WithSummary("Register a new webhook source — the returned API key is shown exactly once")
                 .Produces<CreateWebhookSourceResponse>(StatusCodes.Status201Created)
                 .Produces(StatusCodes.Status409Conflict);

        private static async Task<IResult> HandleAsync(
            ISender sender, [FromBody] CreateWebhookSourceRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(request.ToCommand(), cancellationToken);

            return result.Match(created =>
            {
                var response = CreateWebhookSourceResponse.From(created);
                return Results.Created($"/api/v1/admin/webhook-sources/{response.Id}", response);
            });
        }
    }
}