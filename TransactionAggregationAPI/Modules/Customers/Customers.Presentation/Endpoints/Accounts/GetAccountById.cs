using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Customers.Application.Features.GetAccountById;
using Modules.Customers.Presentation.Responses;

namespace Modules.Customers.Presentation.Endpoints.Accounts
{
    internal static class GetAccountById
    {
        public static RouteHandlerBuilder MapGetAccountById(this IEndpointRouteBuilder group) =>
            group.MapGet("/{accountId:guid}", HandleAsync)
                 .WithName("GetAccountById")
                 .WithSummary("Get a specific account by ID")
                 .Produces<AccountResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(
            ISender sender, Guid customerId, Guid accountId, CancellationToken cancellationToken)
        {
            // customerId is the caller (group filter); the query only matches their own accounts.
            var result = await sender.Send(new GetAccountByIdQuery(accountId, customerId), cancellationToken);

            return result.ToOk(AccountResponse.From);
        }
    }
}