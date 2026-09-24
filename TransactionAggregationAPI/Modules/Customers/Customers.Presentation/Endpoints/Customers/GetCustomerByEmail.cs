using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Customers.Application.Features.GetCustomerByEmail;
using Modules.Customers.Presentation.Responses;
using SharedKernel.Abstractions.Authentication;

namespace Modules.Customers.Presentation.Endpoints.Customers
{
    internal static class GetCustomerByEmail
    {
        public static RouteHandlerBuilder MapGetCustomerByEmail(this IEndpointRouteBuilder group) =>
            group.MapGet("/email/{email}", HandleAsync)
                 .WithName("GetCustomerByEmail")
                 .WithSummary("Get a customer by email address")
                 .Produces<CustomerResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(
            ISender sender, IUserContext userContext, string email, CancellationToken cancellationToken)
        {
            // Scoped to the caller inside the query: someone else's email and an unregistered
            // one are indistinguishable, in both response and timing.
            var result = await sender.Send(new GetCustomerByEmailQuery(email, userContext.UserId), cancellationToken);

            return result.ToOk(CustomerResponse.From);
        }
    }
}