using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.BankLinks.Application.Features.GetBankLinks;
using Modules.BankLinks.Presentation.Responses;

namespace Modules.BankLinks.Presentation.Endpoints
{
    internal static class GetBankLinks
    {
        public static RouteHandlerBuilder MapGetBankLinks(this IEndpointRouteBuilder group) =>
            group.MapGet("/", HandleAsync)
                 .WithName("GetBankLinks")
                 .WithSummary("List the customer's linked bank accounts and their status")
                 .Produces<IReadOnlyList<BankLinkResponse>>(StatusCodes.Status200OK);

        private static async Task<IResult> HandleAsync(ISender sender, Guid customerId, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetBankLinksQuery(customerId), cancellationToken);

            return result.ToOk(links => links.Select(BankLinkResponse.From).ToList());
        }
    }
}