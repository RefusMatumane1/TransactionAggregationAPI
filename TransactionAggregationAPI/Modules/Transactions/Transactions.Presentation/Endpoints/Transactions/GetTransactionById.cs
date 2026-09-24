using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransaction;
using Modules.Transactions.Presentation.Responses;
using SharedKernel.Abstractions.Authentication;

namespace Modules.Transactions.Presentation.Endpoints.Transactions
{
    internal static class GetTransactionById
    {
        public static RouteHandlerBuilder MapGetTransactionById(this IEndpointRouteBuilder group) =>
            group.MapGet("/{id:guid}", HandleAsync)
                 .WithName("GetTransactionById")
                 .WithSummary("Get a specific transaction by ID")
                 .Produces<TransactionResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(
            ISender sender, IUserContext userContext, Guid id, CancellationToken cancellationToken)
        {
            // Scoped to the caller inside the query, so another customer's transaction and a
            // missing one are indistinguishable, in both response and timing.
            var result = await sender.Send(new GetTransactionQuery(id, userContext.UserId), cancellationToken);

            return result.ToOk(TransactionResponse.From);
        }
    }
}