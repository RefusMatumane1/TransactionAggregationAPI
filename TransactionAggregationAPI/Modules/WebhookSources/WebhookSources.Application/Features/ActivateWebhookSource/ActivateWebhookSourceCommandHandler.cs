using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Abstractions.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Audit.Contracts;
using Modules.WebhookSources.Application.Common;
using Modules.WebhookSources.Application.Persistence;
using Modules.WebhookSources.Domain.ValueObjects;
using SharedKernel.Common.Models;

namespace Modules.WebhookSources.Application.Features.ActivateWebhookSource
{
    internal sealed class ActivateWebhookSourceCommandHandler(
        IWebhookSourcesDbContext context,
        IUserContext userContext,
        ILogger<ActivateWebhookSourceCommandHandler> logger)
        : ICommandHandler<ActivateWebhookSourceCommand>
    {
        public async Task<Result> Handle(ActivateWebhookSourceCommand request, CancellationToken cancellationToken)
        {
            var id = WebhookSourceId.CreateFrom(request.Id);
            var source = await context.WebhookSources
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

            if (source is null)
                return Result.Failure(Error.NotFound("WebhookSource", request.Id));

            source.Activate();
            context.StageAudit([AdminAudit.Of(AuditEventTypes.SourceActivated, source, userContext.UserId, "Activated: deliveries with its key are accepted")]);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Webhook source {SourceName} ({SourceId}) activated by admin {AdminId}",
                source.Name, source.Id.Value, userContext.UserId);

            return Result.Success();
        }
    }
}