using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Web;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Currency.Contracts;
using Modules.Customers.Contracts;
using Modules.Reporting.Presentation.Requests;
using Modules.Reporting.Presentation.Responses;
using SharedKernel.Common.Models;

namespace Modules.Reporting.Presentation.Endpoints
{
    internal static class CustomerAggregates
    {
        public static IEndpointRouteBuilder MapCustomerAggregates(this IEndpointRouteBuilder group)
        {
            group.MapGet("/{customerId:guid}/aggregates/categories", CategoriesAsync)
                 .WithName("GetCustomerCategoryBreakdown")
                 .AddEndpointFilter<ViewAuditFilter<ICustomerViewAudit>>()
                 .WithSummary("The customer's spending (or income) per category across their banks")
                 .Produces<CategoryBreakdownResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound)
                 .ProducesValidationProblem();

            group.MapGet("/{customerId:guid}/aggregates/cash-flow", CashFlowAsync)
                 .WithName("GetCustomerCashFlow")
                 .AddEndpointFilter<ViewAuditFilter<ICustomerViewAudit>>()
                 .WithSummary("The customer's income, expenses and net per day, week or month across their banks")
                 .Produces<CashFlowSeriesResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound)
                 .ProducesValidationProblem();

            group.MapGet("/{customerId:guid}/aggregates/institutions", InstitutionsAsync)
                 .WithName("GetCustomerInstitutionBreakdown")
                 .AddEndpointFilter<ViewAuditFilter<ICustomerViewAudit>>()
                 .WithSummary("The customer's totals per bank, then per account")
                 .Produces<InstitutionBreakdownResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound)
                 .ProducesValidationProblem();

            group.MapGet("/{customerId:guid}/aggregates/comparison", ComparisonAsync)
                 .WithName("GetCustomerPeriodComparison")
                 .AddEndpointFilter<ViewAuditFilter<ICustomerViewAudit>>()
                 .WithSummary("The customer's period against the equally long period before it")
                 .Produces<PeriodComparisonResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound)
                 .ProducesValidationProblem();

            return group;
        }

        private static async Task<IResult> CategoriesAsync(
            ISender sender, IUserContext user, ICustomerAccounts customers, TimeProvider time, ICurrencyCatalog catalog, Guid customerId,
            [AsParameters] CategoryBreakdownRequest request, CancellationToken cancellationToken)
        {
            if (await customers.FindVisibleAsync(customerId, user.InstitutionAccess, cancellationToken) is not { } accounts)
                return NotFound(customerId);

            var query = request.ToQuery(user.InstitutionAccess, time, catalog);
            return (await sender.Send(query with { Filter = query.Filter with { Accounts = accounts } }, cancellationToken))
                .ToOk(CategoryBreakdownResponse.From);
        }

        private static async Task<IResult> CashFlowAsync(
            ISender sender, IUserContext user, ICustomerAccounts customers, TimeProvider time, ICurrencyCatalog catalog, Guid customerId,
            [AsParameters] CashFlowRequest request, CancellationToken cancellationToken)
        {
            if (await customers.FindVisibleAsync(customerId, user.InstitutionAccess, cancellationToken) is not { } accounts)
                return NotFound(customerId);

            var query = request.ToQuery(user.InstitutionAccess, time, catalog);
            return (await sender.Send(query with { Filter = query.Filter with { Accounts = accounts } }, cancellationToken))
                .ToOk(CashFlowSeriesResponse.From);
        }

        private static async Task<IResult> InstitutionsAsync(
            ISender sender, IUserContext user, ICustomerAccounts customers, TimeProvider time, ICurrencyCatalog catalog, Guid customerId,
            [AsParameters] InstitutionBreakdownRequest request, CancellationToken cancellationToken)
        {
            if (await customers.FindVisibleAsync(customerId, user.InstitutionAccess, cancellationToken) is not { } accounts)
                return NotFound(customerId);

            var query = request.ToQuery(user.InstitutionAccess, time, catalog);
            return (await sender.Send(query with { Filter = query.Filter with { Accounts = accounts } }, cancellationToken))
                .ToOk(InstitutionBreakdownResponse.From);
        }

        private static async Task<IResult> ComparisonAsync(
            ISender sender, IUserContext user, ICustomerAccounts customers, TimeProvider time, ICurrencyCatalog catalog, Guid customerId,
            [AsParameters] PeriodComparisonRequest request, CancellationToken cancellationToken)
        {
            if (await customers.FindVisibleAsync(customerId, user.InstitutionAccess, cancellationToken) is not { } accounts)
                return NotFound(customerId);

            var query = request.ToQuery(user.InstitutionAccess, time, catalog);
            return (await sender.Send(query with { Filter = query.Filter with { Accounts = accounts } }, cancellationToken))
                .ToOk(PeriodComparisonResponse.From);
        }

        private static IResult NotFound(Guid customerId) =>
            CustomResults.Problem(Result.Failure(Error.NotFound("Customer", customerId)));
    }
}
