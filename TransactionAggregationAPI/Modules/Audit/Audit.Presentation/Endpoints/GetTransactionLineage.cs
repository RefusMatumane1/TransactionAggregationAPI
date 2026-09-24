using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Audit.Application.Features.GetTransactionLineage;
using Modules.Audit.Presentation.Responses;

namespace Modules.Audit.Presentation.Endpoints
{
    internal static class GetTransactionLineage
    {
        public static RouteHandlerBuilder MapGetTransactionLineage(this IEndpointRouteBuilder group) =>
            group.MapGet("/transactions/{transactionId:guid}/lineage", HandleAsync)
                 .WithName("GetTransactionLineage")
                 .WithSummary("Where a transaction came from: channel, source, delivery metadata and every event of the delivery that carried it")
                 .Produces<TransactionLineageResponse>(StatusCodes.Status200OK)
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(ISender sender, Guid transactionId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetTransactionLineageQuery(transactionId), cancellationToken);

            return result.ToOk(TransactionLineageResponse.From);
        }
    }
}