using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Transactions.Presentation.Requests;

namespace Modules.Transactions.Presentation.Endpoints.CustomerTransactions
{
    internal static class ExportCustomerTransactions
    {
        public static RouteHandlerBuilder MapExportCustomerTransactions(this IEndpointRouteBuilder group) =>
            group.MapGet("/export", HandleAsync)
                 .WithName("ExportCustomerTransactions")
                 .WithSummary("Export transactions to CSV with optional date range and category filter")
                 .Produces(StatusCodes.Status200OK, contentType: "text/csv")
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(
            ISender sender,
            Guid customerId,
            [AsParameters] ExportTransactionsRequest request,
            CancellationToken cancellationToken)
        {
            var result = await sender.Send(request.ToQuery(customerId), cancellationToken);

            return result.Match(export => Results.File(export.Content, export.ContentType, export.FileName));
        }
    }
}