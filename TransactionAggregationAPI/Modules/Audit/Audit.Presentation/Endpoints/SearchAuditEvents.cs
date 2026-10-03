using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Audit.Presentation.Requests;
using Modules.Audit.Presentation.Responses;

namespace Modules.Audit.Presentation.Endpoints
{
    internal static class SearchAuditEvents
    {
        public static RouteHandlerBuilder MapSearchAuditEvents(this IEndpointRouteBuilder group) =>
            group.MapGet("/events", HandleAsync)
                 .WithName("SearchAuditEvents")
                 .WithSummary("Search the inbound audit trail by channel, source, event type, account, delivery, transaction or time range (newest first)")
                 .Produces<CursorPagedResponse<AuditEventResponse>>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status400BadRequest);

        private static async Task<IResult> HandleAsync(
            ISender sender, [AsParameters] SearchAuditEventsRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(request.ToQuery(), cancellationToken);

            return result.ToOk(page => CursorPagedResponse<AuditEventResponse>.From(page, AuditEventResponse.From));
        }
    }
}