using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Web;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Currency.Contracts;
using Modules.Reporting.Presentation.Requests;
using Modules.Reporting.Presentation.Responses;

namespace Modules.Reporting.Presentation.Endpoints
{
    internal static class GetTransactionSummary
    {
        public static RouteHandlerBuilder MapGetTransactionSummary(this IEndpointRouteBuilder group) =>
            group.MapGet("/summary", HandleAsync)
                 .WithName("GetTransactionSummary")
                 .WithSummary("Total income/expenses, spend per category and monthly breakdowns, optionally for one bank or account")
                 .Produces<TransactionSummaryResponse>(StatusCodes.Status200OK)
                 .ProducesValidationProblem();

        private static async Task<IResult> HandleAsync(
            ISender sender, IUserContext user, TimeProvider time, ICurrencyCatalog catalog, [AsParameters] TransactionSummaryRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(request.ToQuery(user.InstitutionAccess, time, catalog), cancellationToken);

            return result.ToOk(TransactionSummaryResponse.From);
        }
    }
}