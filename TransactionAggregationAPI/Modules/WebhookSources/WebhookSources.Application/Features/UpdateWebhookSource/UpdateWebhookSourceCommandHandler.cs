using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Abstractions.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Audit.Contracts;
using Modules.WebhookSources.Application.Common;
using Modules.WebhookSources.Application.Persistence;
using Modules.WebhookSources.Domain.ValueObjects;
using SharedKernel.Common.Models;

namespace Modules.WebhookSources.Application.Features.UpdateWebhookSource
{
    internal sealed class UpdateWebhookSourceCommandHandler(
        IWebhookSourcesDbContext context,
        IUserContext userContext,
        ILogger<UpdateWebhookSourceCommandHandler> logger)
        : ICommandHandler<UpdateWebhookSourceCommand>
    {
        public async Task<Result> Handle(UpdateWebhookSourceCommand request, CancellationToken cancellationToken)
        {
            var id = WebhookSourceId.CreateFrom(request.Id);
            var source = await context.WebhookSources
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

            if (source is null)
                return Result.Failure(Error.NotFound("WebhookSource", request.Id));

            source.UpdateDetails(request.DisplayName, request.Color);
            context.StageAudit([AdminAudit.Of(AuditEventTypes.SourceUpdated, source, userContext.UserId, $"Display name '{source.DisplayName}', colour {source.Color}")]);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Bank source {SourceName} ({SourceId}) details updated by admin {AdminId}",
                source.Name, source.Id.Value, userContext.UserId);

            return Result.Success();
        }
    }
}