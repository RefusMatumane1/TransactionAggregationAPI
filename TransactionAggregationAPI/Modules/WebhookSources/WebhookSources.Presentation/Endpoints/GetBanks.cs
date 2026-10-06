using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.WebhookSources.Application.Features.GetBanks;
using Modules.WebhookSources.Presentation.Responses;

namespace Modules.WebhookSources.Presentation.Endpoints
{
    internal static class GetBanks
    {
        public static RouteHandlerBuilder MapGetBanks(this IEndpointRouteBuilder group) =>
            group.MapGet("/", HandleAsync)
                 .WithName("GetBanks")
                 .WithSummary("The banks the caller may read (all of them for admins): code, display name, colour, status and last delivery")
                 .Produces<IReadOnlyList<BankResponse>>(StatusCodes.Status200OK);

        private static async Task<IResult> HandleAsync(ISender sender, IUserContext user, CancellationToken cancellationToken)
        {
            var result = await sender.Send(new GetBanksQuery(user.InstitutionAccess), cancellationToken);

            return result.ToOk(banks => banks.Select(BankResponse.From).ToList());
        }
    }
}