using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Transactions.Presentation.Requests;
using Modules.Transactions.Presentation.Responses;

namespace Modules.Transactions.Presentation.Endpoints.CustomerTransactions
{
    internal static class GetCustomerWithTransactions
    {
        public static RouteHandlerBuilder MapGetCustomerWithTransactions(this IEndpointRouteBuilder group) =>
            group.MapGet(string.Empty, HandleAsync)
                 .WithName("GetCustomerWithTransactions")
                 .WithSummary("Get customer with their transactions")
                 .Produces<CustomerWithTransactionsResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(
            ISender sender,
            Guid customerId,
            [AsParameters] GetCustomerWithTransactionsRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(request.ToQuery(customerId), cancellationToken);

            return result.ToOk(CustomerWithTransactionsResponse.From);
        }
    }
}