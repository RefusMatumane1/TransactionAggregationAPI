using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Transactions.Presentation.Requests;
using Modules.Transactions.Presentation.Responses;

namespace Modules.Transactions.Presentation.Endpoints.Transactions
{
    internal static class ListTransactions
    {
        public static RouteHandlerBuilder MapListTransactions(this IEndpointRouteBuilder group) =>
            group.MapGet("/", HandleAsync)
                 .WithName("ListTransactions")
                 .WithSummary("Cursor-paginated ledger entries the caller may read, filtered by bank, account, date, category, currency, amount and description search")
                 .Produces<CursorPagedResponse<TransactionListItemResponse>>(StatusCodes.Status200OK)
                 .ProducesValidationProblem();

        private static async Task<IResult> HandleAsync(
            ISender sender, IUserContext user, [AsParameters] FilterTransactionsRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(request.ToQuery(user.InstitutionAccess), cancellationToken);

            return result.ToOk(page => CursorPagedResponse<TransactionListItemResponse>.From(page, TransactionListItemResponse.From));
        }
    }
}