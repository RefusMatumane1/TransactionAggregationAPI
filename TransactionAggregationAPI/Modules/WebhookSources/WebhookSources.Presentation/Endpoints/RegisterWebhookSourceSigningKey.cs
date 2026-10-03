using BuildingBlocks.Web;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Modules.WebhookSources.Presentation.Requests;

namespace Modules.WebhookSources.Presentation.Endpoints
{
    internal static class RegisterWebhookSourceSigningKey
    {
        public static RouteHandlerBuilder MapRegisterWebhookSourceSigningKey(this IEndpointRouteBuilder group) =>
            group.MapPut("/{id:guid}/signing-key", HandleAsync)
                 .WithName("RegisterWebhookSourceSigningKey")
                 .WithSummary("Register the ECDSA P-256 public key that verifies the source's Kafka records")
                 .Produces(StatusCodes.Status204NoContent)
                 .Produces(StatusCodes.Status400BadRequest)
                 .Produces(StatusCodes.Status404NotFound);

        private static async Task<IResult> HandleAsync(
            ISender sender, Guid id, [FromBody] RegisterWebhookSourceSigningKeyRequest request, CancellationToken cancellationToken)
        {
            var result = await sender.Send(request.ToCommand(id), cancellationToken);
            return result.ToNoContent();
        }
    }
}