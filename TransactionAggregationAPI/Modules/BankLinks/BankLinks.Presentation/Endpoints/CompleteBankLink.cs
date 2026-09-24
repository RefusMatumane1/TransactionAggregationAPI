using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.BankLinks.Presentation.Requests;
using Modules.BankLinks.Presentation.Responses;

namespace Modules.BankLinks.Presentation.Endpoints
{
    internal static class CompleteBankLink
    {
        public static RouteHandlerBuilder MapCompleteBankLink(this IEndpointRouteBuilder group) =>
            group.MapGet("/callback", HandleAsync)
                 .WithName("CompleteBankLink")
                 .WithSummary("OAuth redirect target the aggregator sends the customer's browser back to")
                 .Produces<CompleteBankLinkResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status400BadRequest);

        private static async Task<IResult> HandleAsync(
            ISender sender, [AsParameters] CompleteBankLinkRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(request.ToCommand(), cancellationToken);

            return result.ToOk(accountId => new CompleteBankLinkResponse(accountId));
        }
    }
}