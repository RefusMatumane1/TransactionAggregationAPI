using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Currency.Contracts;
using Modules.Customers.Contracts;
using Modules.Transactions.Presentation.Requests;
using Modules.Transactions.Presentation.Responses;
using SharedKernel.Common.Models;

namespace Modules.Transactions.Presentation.Endpoints.Customers
{
    internal static class CustomerTransactions
    {
        public static IEndpointRouteBuilder MapCustomerTransactions(this IEndpointRouteBuilder group)
        {
            group.MapGet("/{customerId:guid}/transactions", ListAsync)
                 .WithName("ListCustomerTransactions")
                 .AddEndpointFilter<ViewAuditFilter<ICustomerViewAudit>>()
                 .WithSummary("The customer's ledger entries across all their linked accounts (at banks the caller may read), with the same filters and paging as /transactions")
                 .Produces<CursorPagedResponse<TransactionListItemResponse>>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound)
                 .ProducesValidationProblem();

            return group;
        }

        private static async Task<IResult> ListAsync(
            ISender sender, IUserContext user, ICustomerAccounts customers, ICurrencyCatalog catalog, Guid customerId,
            [AsParameters] FilterTransactionsRequest request, CancellationToken cancellationToken)
        {
            if (await customers.FindVisibleAsync(customerId, user.InstitutionAccess, cancellationToken) is not { } accounts)
                return CustomResults.Problem(Result.Failure(Error.NotFound("Customer", customerId)));

            var query = request.ToQuery(user.InstitutionAccess, catalog);
            var result = await sender.Send(query with { Filter = query.Filter with { Accounts = accounts } }, cancellationToken);
            return result.ToOk(page => CursorPagedResponse<TransactionListItemResponse>.From(page, TransactionListItemResponse.From));
        }
    }
}
