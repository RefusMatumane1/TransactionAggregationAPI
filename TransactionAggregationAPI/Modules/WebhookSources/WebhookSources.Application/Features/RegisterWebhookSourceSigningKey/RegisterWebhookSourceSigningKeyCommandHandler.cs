using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Abstractions.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Audit.Contracts;
using Modules.WebhookSources.Application.Common;
using Modules.WebhookSources.Application.Persistence;
using Modules.WebhookSources.Domain.ValueObjects;
using SharedKernel.Common.Models;

namespace Modules.WebhookSources.Application.Features.RegisterWebhookSourceSigningKey
{
    internal sealed class RegisterWebhookSourceSigningKeyCommandHandler(
        IWebhookSourcesDbContext context,
        IUserContext userContext,
        ILogger<RegisterWebhookSourceSigningKeyCommandHandler> logger)
        : ICommandHandler<RegisterWebhookSourceSigningKeyCommand>
    {
        public async Task<Result> Handle(RegisterWebhookSourceSigningKeyCommand request, CancellationToken cancellationToken)
        {
            var id = WebhookSourceId.CreateFrom(request.Id);
            var source = await context.WebhookSources
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

            if (source is null)
                return Result.Failure(Error.NotFound("WebhookSource", request.Id));

            source.RegisterSigningPublicKey(request.PublicKey);
            context.StageAudit([AdminAudit.Of(AuditEventTypes.SourceSigningKeyRegistered, source, userContext.UserId, "Kafka record signing key registered")]);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Webhook source {SourceName} ({SourceId}) signing key registered by admin {AdminId}",
                source.Name, source.Id.Value, userContext.UserId);

            return Result.Success();
        }
    }
}