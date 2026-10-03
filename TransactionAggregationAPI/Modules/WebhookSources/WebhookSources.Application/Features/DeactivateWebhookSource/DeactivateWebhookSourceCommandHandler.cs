using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Abstractions.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Audit.Contracts;
using Modules.WebhookSources.Application.Common;
using Modules.WebhookSources.Application.Persistence;
using Modules.WebhookSources.Domain.ValueObjects;
using SharedKernel.Common.Models;

namespace Modules.WebhookSources.Application.Features.DeactivateWebhookSource
{
    internal sealed class DeactivateWebhookSourceCommandHandler(
        IWebhookSourcesDbContext context,
        IUserContext userContext,
        ILogger<DeactivateWebhookSourceCommandHandler> logger)
        : ICommandHandler<DeactivateWebhookSourceCommand>
    {
        public async Task<Result> Handle(DeactivateWebhookSourceCommand request, CancellationToken cancellationToken)
        {
            var id = WebhookSourceId.CreateFrom(request.Id);
            var source = await context.WebhookSources
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

            if (source is null)
                return Result.Failure(Error.NotFound("WebhookSource", request.Id));

            source.Deactivate();
            context.StageAudit([AdminAudit.Of(AuditEventTypes.SourceDeactivated, source, userContext.UserId, "Deactivated: deliveries with its key are refused")]);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Webhook source {SourceName} ({SourceId}) deactivated by admin {AdminId}",
                source.Name, source.Id.Value, userContext.UserId);

            return Result.Success();
        }
    }
}