using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Abstractions;
using SharedKernel.Abstractions.Authentication;
using SharedKernel.Common.Models;
using Modules.WebhookSources.Persistence;
using Modules.WebhookSources.ValueObjects;

namespace Modules.WebhookSources.Features.DeactivateWebhookSource
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
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Webhook source {SourceName} ({SourceId}) deactivated by admin {AdminId}",
                source.Name, source.Id.Value, userContext.UserId);

            return Result.Success();
        }
    }
}
