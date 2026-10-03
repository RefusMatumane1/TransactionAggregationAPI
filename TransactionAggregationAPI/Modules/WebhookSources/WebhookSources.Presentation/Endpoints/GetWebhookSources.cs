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
                 .WithSummary("List webhook sources by name (cursor-paginated; never includes key material)")
                 .Produces<CursorPagedResponse<WebhookSourceResponse>>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status400BadRequest);

        private static async Task<IResult> HandleAsync(
            ISender sender, string? cursor, int? pageSize, CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new GetWebhookSourcesQuery(cursor, pageSize ?? GetWebhookSourcesQuery.DefaultPageSize), cancellationToken);

            return result.ToOk(page => CursorPagedResponse<WebhookSourceResponse>.From(page, WebhookSourceResponse.From));
        }
    }
}