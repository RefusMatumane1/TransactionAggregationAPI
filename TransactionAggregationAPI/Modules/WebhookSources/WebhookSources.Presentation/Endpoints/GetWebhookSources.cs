using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.WebhookSources.Application.Features.GetWebhookSources;
using Modules.WebhookSources.Presentation.Responses;

namespace Modules.WebhookSources.Presentation.Endpoints
{
    internal static class GetWebhookSources
    {
        public static RouteHandlerBuilder MapGetWebhookSources(this IEndpointRouteBuilder group) =>
            group.MapGet("/", HandleAsync)
                 .WithName("GetWebhookSources")
                 .WithSummary("List all webhook sources (never includes key material)")
                 .Produces<IReadOnlyList<WebhookSourceResponse>>(StatusCodes.Status200OK);

        private static async Task<IResult> HandleAsync(ISender sender, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetWebhookSourcesQuery(), cancellationToken);

            return result.ToOk(sources => sources.Select(WebhookSourceResponse.From).ToList());
        }
    }
}