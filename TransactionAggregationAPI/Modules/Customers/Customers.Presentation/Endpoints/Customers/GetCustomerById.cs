using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Customers.Application.Features.GetCustomer;
using Modules.Customers.Presentation.Responses;

namespace Modules.Customers.Presentation.Endpoints.Customers
{
    internal static class GetCustomerById
    {
        public static RouteHandlerBuilder MapGetCustomerById(this IEndpointRouteBuilder group) =>
            group.MapGet(string.Empty, HandleAsync)
                 .WithName("GetCustomerById")
                 .WithSummary("Get a specific customer by ID")
                 .Produces<CustomerResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(ISender sender, Guid customerId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetCustomerQuery(customerId), cancellationToken);

            return result.ToOk(CustomerResponse.From);
        }
    }
}