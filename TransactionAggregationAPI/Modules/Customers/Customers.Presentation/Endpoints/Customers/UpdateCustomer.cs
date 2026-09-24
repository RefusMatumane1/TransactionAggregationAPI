using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Customers.Presentation.Requests;

namespace Modules.Customers.Presentation.Endpoints.Customers
{
    internal static class UpdateCustomer
    {
        public static RouteHandlerBuilder MapUpdateCustomer(this IEndpointRouteBuilder group) =>
            group.MapPut(string.Empty, HandleAsync)
                 .WithName("UpdateCustomer")
                 .WithSummary("Update an existing customer")
                 .Accepts<UpdateCustomerRequest>("application/json")
                 .Produces(StatusCodes.Status204NoContent)
                 .Produces(StatusCodes.Status404NotFound)
                 .Produces(StatusCodes.Status400BadRequest);

        private static async Task<IResult> HandleAsync(
            ISender sender, Guid customerId, [FromBody] UpdateCustomerRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(request.ToCommand(customerId), cancellationToken);

            return result.ToNoContent();
        }
    }
}