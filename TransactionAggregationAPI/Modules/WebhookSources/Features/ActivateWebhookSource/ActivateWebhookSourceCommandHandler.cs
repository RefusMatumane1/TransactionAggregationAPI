using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Abstractions;
using SharedKernel.Abstractions.Authentication;
using SharedKernel.Common.Models;
using Modules.WebhookSources.Persistence;
using Modules.WebhookSources.ValueObjects;

namespace Modules.WebhookSources.Features.ActivateWebhookSource
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
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Webhook source {SourceName} ({SourceId}) activated by admin {AdminId}",
                source.Name, source.Id.Value, userContext.UserId);

            return Result.Success();
        }
    }
}
