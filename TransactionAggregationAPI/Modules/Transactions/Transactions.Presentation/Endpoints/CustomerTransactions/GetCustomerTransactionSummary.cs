using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Transactions.Presentation.Requests;
using Modules.Transactions.Presentation.Responses;

namespace Modules.Transactions.Presentation.Endpoints.CustomerTransactions
{
    internal static class GetCustomerTransactionSummary
    {
        public static RouteHandlerBuilder MapGetCustomerTransactionSummary(this IEndpointRouteBuilder group) =>
            group.MapGet("/summary", HandleAsync)
                 .WithName("GetCustomerTransactionSummary")
                 .WithSummary("Get spending summary: total income/expenses, spend per category, and monthly breakdowns")
                 .Produces<TransactionSummaryResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(
            ISender sender,
            Guid customerId,
            [AsParameters] TransactionSummaryRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(request.ToQuery(customerId), cancellationToken);

            return result.ToOk(TransactionSummaryResponse.From);
        }
    }
}