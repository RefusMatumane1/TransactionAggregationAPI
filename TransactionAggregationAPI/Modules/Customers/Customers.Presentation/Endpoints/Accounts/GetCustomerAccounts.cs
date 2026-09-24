using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Customers.Application.Features.GetCustomerAccounts;
using Modules.Customers.Presentation.Responses;

namespace Modules.Customers.Presentation.Endpoints.Accounts
{
    internal static class GetCustomerAccounts
    {
        public static RouteHandlerBuilder MapGetCustomerAccounts(this IEndpointRouteBuilder group) =>
            group.MapGet("/", HandleAsync)
                 .WithName("GetCustomerAccounts")
                 .WithSummary("Get all accounts for a customer")
                 .Produces<IEnumerable<AccountResponse>>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(ISender sender, Guid customerId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetCustomerAccountsQuery(customerId), cancellationToken);

            return result.ToOk(accounts => accounts.Select(AccountResponse.From).ToList());
        }
    }
}