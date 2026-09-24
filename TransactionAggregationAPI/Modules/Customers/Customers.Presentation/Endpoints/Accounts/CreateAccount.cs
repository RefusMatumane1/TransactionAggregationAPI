using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Customers.Presentation.Requests;

namespace Modules.Customers.Presentation.Endpoints.Accounts
{
    internal static class CreateAccount
    {
        public static RouteHandlerBuilder MapCreateAccount(this IEndpointRouteBuilder group) =>
            group.MapPost("/", HandleAsync)
                 .WithName("CreateAccount")
                 .WithSummary("Create a new account for a customer")
                 .Accepts<CreateAccountRequest>("application/json")
                 .Produces<Guid>(StatusCodes.Status201Created)
                 .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(
            ISender sender, Guid customerId, [FromBody] CreateAccountRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(request.ToCommand(customerId), cancellationToken);

            return result.Match(accountId =>
                Results.Created($"/api/v1/customers/{customerId}/accounts/{accountId}", accountId));
        }
    }
}