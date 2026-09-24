using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.BankLinks.Presentation.Requests;
using Modules.BankLinks.Presentation.Responses;

namespace Modules.BankLinks.Presentation.Endpoints
{
    internal static class InitiateBankLink
    {
        public static RouteHandlerBuilder MapInitiateBankLink(this IEndpointRouteBuilder group) =>
            group.MapPost("/", HandleAsync)
                 .WithName("InitiateBankLink")
                 .WithSummary("Start the consent flow to link a South African bank account via the account aggregator")
                 .Produces<InitiateBankLinkResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound)
                 .Produces(StatusCodes.Status409Conflict);

        private static async Task<IResult> HandleAsync(
            ISender sender, Guid customerId, [FromBody] InitiateBankLinkRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(request.ToCommand(customerId), cancellationToken);

            return result.ToOk(authorizationUrl => new InitiateBankLinkResponse(authorizationUrl));
        }
    }
}