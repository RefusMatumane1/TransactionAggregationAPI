using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Abstractions.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Audit.Contracts;
using Modules.WebhookSources.Application.Common;
using Modules.WebhookSources.Application.Persistence;
using Modules.WebhookSources.Domain.ValueObjects;
using SharedKernel.Common.Models;

namespace Modules.WebhookSources.Application.Features.RotateWebhookSourceKey
{
    internal sealed class RotateWebhookSourceKeyCommandHandler(
        IWebhookSourcesDbContext context,
        IUserContext userContext,
        ILogger<RotateWebhookSourceKeyCommandHandler> logger)
        : ICommandHandler<RotateWebhookSourceKeyCommand, string>
    {
        public async Task<Result<string>> Handle(RotateWebhookSourceKeyCommand request, CancellationToken cancellationToken)
        {
            var id = WebhookSourceId.CreateFrom(request.Id);
            var source = await context.WebhookSources
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

            if (source is null)
                return Result.Failure<string>(Error.NotFound("WebhookSource", request.Id));

            var apiKey = source.RotateKey();
            context.StageAudit([AdminAudit.Of(AuditEventTypes.SourceKeyRotated, source, userContext.UserId, "Webhook API key rotated; the previous key stopped working")]);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Webhook source {SourceName} ({SourceId}) key rotated by admin {AdminId}",
                source.Name, source.Id.Value, userContext.UserId);

            return Result.Success(apiKey);
        }
    }
}