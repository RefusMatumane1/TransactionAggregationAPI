using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Transactions.Presentation.Requests;
using Modules.Transactions.Presentation.Responses;

namespace Modules.Transactions.Presentation.Endpoints.Reporting
{
    internal static class GetTransactionAggregates
    {
        public static IEndpointRouteBuilder MapTransactionAggregates(this IEndpointRouteBuilder group)
        {
            group.MapGet("/aggregates/categories", CategoriesAsync)
                 .WithName("GetCategoryBreakdown")
                 .WithSummary("Spending (or income) per category in one currency over a South African calendar period, with each category's share")
                 .Produces<CategoryBreakdownResponse>(StatusCodes.Status200OK)
                 .ProducesValidationProblem();

            group.MapGet("/aggregates/cash-flow", CashFlowAsync)
                 .WithName("GetCashFlow")
                 .WithSummary("Income, expenses and net per day, week or month, with empty periods reported as zero")
                 .Produces<CashFlowSeriesResponse>(StatusCodes.Status200OK)
                 .ProducesValidationProblem();

            group.MapGet("/aggregates/institutions", InstitutionsAsync)
                 .WithName("GetInstitutionBreakdown")
                 .WithSummary("Income, expenses, net and account count per bank, then per account")
                 .Produces<InstitutionBreakdownResponse>(StatusCodes.Status200OK)
                 .ProducesValidationProblem();

            group.MapGet("/aggregates/comparison", ComparisonAsync)
                 .WithName("GetPeriodComparison")
                 .WithSummary("A period against the equally long period before it, overall and per spending category")
                 .Produces<PeriodComparisonResponse>(StatusCodes.Status200OK)
                 .ProducesValidationProblem();

            return group;
        }

        private static async Task<IResult> CategoriesAsync(
            ISender sender, IUserContext user, TimeProvider time, [AsParameters] CategoryBreakdownRequest request, CancellationToken cancellationToken) =>
            (await sender.Send(request.ToQuery(user.InstitutionAccess, time), cancellationToken)).ToOk(CategoryBreakdownResponse.From);

        private static async Task<IResult> CashFlowAsync(
            ISender sender, IUserContext user, TimeProvider time, [AsParameters] CashFlowRequest request, CancellationToken cancellationToken) =>
            (await sender.Send(request.ToQuery(user.InstitutionAccess, time), cancellationToken)).ToOk(CashFlowSeriesResponse.From);

        private static async Task<IResult> InstitutionsAsync(
            ISender sender, IUserContext user, TimeProvider time, [AsParameters] InstitutionBreakdownRequest request, CancellationToken cancellationToken) =>
            (await sender.Send(request.ToQuery(user.InstitutionAccess, time), cancellationToken)).ToOk(InstitutionBreakdownResponse.From);

        private static async Task<IResult> ComparisonAsync(
            ISender sender, IUserContext user, TimeProvider time, [AsParameters] PeriodComparisonRequest request, CancellationToken cancellationToken) =>
            (await sender.Send(request.ToQuery(user.InstitutionAccess, time), cancellationToken)).ToOk(PeriodComparisonResponse.From);
    }
}