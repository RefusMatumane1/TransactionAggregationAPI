using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Transactions.Application.Common.Currency;
using Modules.Transactions.Application.Features.Transactions.Queries.GetTransaction;
using Modules.Transactions.Presentation.Responses;

namespace Modules.Transactions.Presentation.Endpoints.Transactions
{
    internal static class GetTransactionById
    {
        public static RouteHandlerBuilder MapGetTransactionById(this IEndpointRouteBuilder group) =>
            group.MapGet("/{id:guid}", HandleAsync)
                 .WithName("GetTransactionById")
                 .WithSummary("One ledger entry (add convertTo=USD for its amount in that currency); 404 when it does not exist or belongs to an institution the caller may not read")
                 .Produces<TransactionResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(
            ISender sender, IUserContext user, ICurrencyCatalog catalog, Guid id, string? convertTo, CancellationToken cancellationToken)
        {
            var result = await sender.Send(
                new GetTransactionQuery(id, user.InstitutionAccess)
                {
                    ConvertTo = convertTo,
                    RatesVersion = convertTo is null ? null : catalog.Version
                },
                cancellationToken);

            return result.ToOk(TransactionResponse.From);
        }
    }
}