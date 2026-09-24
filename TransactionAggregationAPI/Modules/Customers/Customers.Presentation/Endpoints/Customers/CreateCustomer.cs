using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Customers.Presentation.Requests;

namespace Modules.Customers.Presentation.Endpoints.Customers
{
    internal static class CreateCustomer
    {
        public static RouteHandlerBuilder MapCreateCustomer(this IEndpointRouteBuilder group) =>
            group.MapPost("/", HandleAsync)
                 .WithName("CreateCustomer")
                 .WithSummary("Create a new customer")
                 .Accepts<CreateCustomerRequest>("application/json")
                 .Produces<Guid>(StatusCodes.Status201Created)
                 .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
                 .Produces(StatusCodes.Status409Conflict)
                 .AllowAnonymous();

        private static async Task<IResult> HandleAsync(
            ISender sender, [FromBody] CreateCustomerRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(request.ToCommand(), cancellationToken);

            return result.Match(customerId => Results.Created($"/api/v1/customers/{customerId}", customerId));
        }
    }
}