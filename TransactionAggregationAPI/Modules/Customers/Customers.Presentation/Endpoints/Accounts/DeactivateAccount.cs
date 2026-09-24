using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Customers.Application.Features.DeactivateAccount;

namespace Modules.Customers.Presentation.Endpoints.Accounts
{
    internal static class DeactivateAccount
    {
        public static RouteHandlerBuilder MapDeactivateAccount(this IEndpointRouteBuilder group) =>
            group.MapPatch("/{accountId:guid}/deactivate", HandleAsync)
                 .WithName("DeactivateAccount")
                 .WithSummary("Deactivate an account")
                 .Produces(StatusCodes.Status204NoContent)
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(
            ISender sender, Guid customerId, Guid accountId, CancellationToken cancellationToken)
        {
            // customerId is the caller (group filter); the command only matches their own accounts.
            var result = await sender.Send(new DeactivateAccountCommand(accountId, customerId), cancellationToken);

            return result.ToNoContent();
        }
    }
}