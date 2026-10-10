using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Customers.Contracts;
using Modules.Transactions.Application.Common.Aggregation;
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
                 .WithSummary("The customer's ledger entries across all their linked accounts (at banks the caller may read), with the same filters and paging as /transactions")
                 .Produces<CursorPagedResponse<TransactionListItemResponse>>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound)
                 .ProducesValidationProblem();

            group.MapGet("/{customerId:guid}/aggregates/categories", CategoriesAsync)
                 .WithName("GetCustomerCategoryBreakdown")
                 .WithSummary("The customer's spending (or income) per category across their banks")
                 .Produces<CategoryBreakdownResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound)
                 .ProducesValidationProblem();

            group.MapGet("/{customerId:guid}/aggregates/cash-flow", CashFlowAsync)
                 .WithName("GetCustomerCashFlow")
                 .WithSummary("The customer's income, expenses and net per day, week or month across their banks")
                 .Produces<CashFlowSeriesResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound)
                 .ProducesValidationProblem();

            group.MapGet("/{customerId:guid}/aggregates/institutions", InstitutionsAsync)
                 .WithName("GetCustomerInstitutionBreakdown")
                 .WithSummary("The customer's totals per bank, then per account")
                 .Produces<InstitutionBreakdownResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound)
                 .ProducesValidationProblem();

            group.MapGet("/{customerId:guid}/aggregates/comparison", ComparisonAsync)
                 .WithName("GetCustomerPeriodComparison")
                 .WithSummary("The customer's period against the equally long period before it")
                 .Produces<PeriodComparisonResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound)
                 .ProducesValidationProblem();

            return group;
        }

        private static async Task<IResult> ListAsync(
            ISender sender, IUserContext user, ICustomerAccounts customers, Guid customerId,
            [AsParameters] FilterTransactionsRequest request, CancellationToken cancellationToken)
        {
            if (await VisibleAccountsAsync(customers, customerId, user.InstitutionAccess, cancellationToken) is not { } accounts)
                return NotFound(customerId);

            var query = request.ToQuery(user.InstitutionAccess);
            var result = await sender.Send(query with { Filter = query.Filter with { Accounts = accounts } }, cancellationToken);
            return result.ToOk(page => CursorPagedResponse<TransactionListItemResponse>.From(page, TransactionListItemResponse.From));
        }

        private static async Task<IResult> CategoriesAsync(
            ISender sender, IUserContext user, ICustomerAccounts customers, TimeProvider time, Guid customerId,
            [AsParameters] CategoryBreakdownRequest request, CancellationToken cancellationToken)
        {
            if (await VisibleAccountsAsync(customers, customerId, user.InstitutionAccess, cancellationToken) is not { } accounts)
                return NotFound(customerId);

            var query = request.ToQuery(user.InstitutionAccess, time);
            return (await sender.Send(query with { Filter = query.Filter with { Accounts = accounts } }, cancellationToken))
                .ToOk(CategoryBreakdownResponse.From);
        }

        private static async Task<IResult> CashFlowAsync(
            ISender sender, IUserContext user, ICustomerAccounts customers, TimeProvider time, Guid customerId,
            [AsParameters] CashFlowRequest request, CancellationToken cancellationToken)
        {
            if (await VisibleAccountsAsync(customers, customerId, user.InstitutionAccess, cancellationToken) is not { } accounts)
                return NotFound(customerId);

            var query = request.ToQuery(user.InstitutionAccess, time);
            return (await sender.Send(query with { Filter = query.Filter with { Accounts = accounts } }, cancellationToken))
                .ToOk(CashFlowSeriesResponse.From);
        }

        private static async Task<IResult> InstitutionsAsync(
            ISender sender, IUserContext user, ICustomerAccounts customers, TimeProvider time, Guid customerId,
            [AsParameters] InstitutionBreakdownRequest request, CancellationToken cancellationToken)
        {
            if (await VisibleAccountsAsync(customers, customerId, user.InstitutionAccess, cancellationToken) is not { } accounts)
                return NotFound(customerId);

            var query = request.ToQuery(user.InstitutionAccess, time);
            return (await sender.Send(query with { Filter = query.Filter with { Accounts = accounts } }, cancellationToken))
                .ToOk(InstitutionBreakdownResponse.From);
        }

        private static async Task<IResult> ComparisonAsync(
            ISender sender, IUserContext user, ICustomerAccounts customers, TimeProvider time, Guid customerId,
            [AsParameters] PeriodComparisonRequest request, CancellationToken cancellationToken)
        {
            if (await VisibleAccountsAsync(customers, customerId, user.InstitutionAccess, cancellationToken) is not { } accounts)
                return NotFound(customerId);

            var query = request.ToQuery(user.InstitutionAccess, time);
            return (await sender.Send(query with { Filter = query.Filter with { Accounts = accounts } }, cancellationToken))
                .ToOk(PeriodComparisonResponse.From);
        }

        internal static async Task<IReadOnlyList<AccountKey>?> VisibleAccountsAsync(
            ICustomerAccounts customers, Guid customerId, InstitutionAccess access, CancellationToken cancellationToken)
        {
            var linked = await customers.FindAsync(customerId, cancellationToken);
            if (linked is null)
                return null;

            var visible = linked
                .Where(a => access.AllInstitutions || access.Institutions.Contains(a.Institution, StringComparer.OrdinalIgnoreCase))
                .Select(a => new AccountKey(a.Institution, a.ExternalAccountId))
                .OrderBy(a => a.Institution, StringComparer.Ordinal)
                .ThenBy(a => a.ExternalAccountId, StringComparer.Ordinal)
                .ToList();

            return visible.Count == 0 && !access.AllInstitutions ? null : visible;
        }

        private static IResult NotFound(Guid customerId) =>
            CustomResults.Problem(Result.Failure(Error.NotFound("Customer", customerId)));
    }
}