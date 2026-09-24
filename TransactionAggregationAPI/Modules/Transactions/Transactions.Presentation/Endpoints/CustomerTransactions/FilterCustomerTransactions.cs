using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Transactions.Presentation.Requests;
using Modules.Transactions.Presentation.Responses;

namespace Modules.Transactions.Presentation.Endpoints.CustomerTransactions
{
    internal static class FilterCustomerTransactions
    {
        public static RouteHandlerBuilder MapFilterCustomerTransactions(this IEndpointRouteBuilder group) =>
            group.MapGet("/filter", HandleAsync)
                 .WithName("FilterCustomerTransactions")
                 .WithSummary("Get paginated transactions with rich filtering: date range, category, status, amount, source, search")
                 .Produces<PagedResponse<TransactionListItemResponse>>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(
            ISender sender,
            Guid customerId,
            [AsParameters] FilterTransactionsRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(request.ToQuery(customerId), cancellationToken);

            return result.ToOk(page => PagedResponse.From(page, TransactionListItemResponse.From));
        }
    }
}