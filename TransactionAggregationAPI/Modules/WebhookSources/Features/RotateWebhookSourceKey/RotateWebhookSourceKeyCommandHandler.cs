using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedKernel.Abstractions;
using SharedKernel.Abstractions.Authentication;
using SharedKernel.Common.Models;
using Modules.WebhookSources.Persistence;
using Modules.WebhookSources.ValueObjects;

namespace Modules.WebhookSources.Features.RotateWebhookSourceKey
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
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Webhook source {SourceName} ({SourceId}) key rotated by admin {AdminId}",
                source.Name, source.Id.Value, userContext.UserId);

            return Result.Success(apiKey);
        }
    }
}
