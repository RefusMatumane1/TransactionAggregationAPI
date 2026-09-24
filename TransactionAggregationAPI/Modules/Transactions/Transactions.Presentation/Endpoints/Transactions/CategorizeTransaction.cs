using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.Transactions.Presentation.Requests;
using SharedKernel.Abstractions.Authentication;

namespace Modules.Transactions.Presentation.Endpoints.Transactions
{
    internal static class CategorizeTransaction
    {
        public static RouteHandlerBuilder MapCategorizeTransaction(this IEndpointRouteBuilder group) =>
            group.MapPatch("/{id:guid}/categorize", HandleAsync)
                 .WithName("CategorizeTransaction")
                 .WithSummary("Manually override the category of a transaction")
                 .Accepts<CategorizeTransactionRequest>("application/json")
                 .Produces(StatusCodes.Status204NoContent)
                 .Produces(StatusCodes.Status404NotFound)
                 .Produces(StatusCodes.Status400BadRequest);

        private static async Task<IResult> HandleAsync(
            ISender sender,
            IUserContext userContext,
            Guid id,
            [FromBody] CategorizeTransactionRequest request,
            CancellationToken cancellationToken)
        {
            // Scoped to the caller inside the command: another customer's transaction is never
            // loaded, let alone changed, and fails exactly like a missing one.
            var result = await sender.Send(request.ToCommand(id, userContext.UserId), cancellationToken);

            return result.ToNoContent();
        }
    }
}