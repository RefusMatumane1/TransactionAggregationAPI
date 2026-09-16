using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Abstractions.Authentication;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Application.Features.WebhookSources.Commands.RotateWebhookSourceKey
{
    internal sealed class RotateWebhookSourceKeyCommandHandler(
        IApplicationDbContext context,
        IUserContext userContext,
        ILogger<RotateWebhookSourceKeyCommandHandler> logger)
        : ICommandHandler<RotateWebhookSourceKeyCommand, string>
    {
        public async Task<Result<string>> Handle(RotateWebhookSourceKeyCommand request, CancellationToken cancellationToken)
        {
            try
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
            catch (Exception ex)
            {
                logger.LogError(ex, "Error rotating webhook source {WebhookSourceId}", request.Id);
                return Result.Failure<string>(Error.Unexpected);
            }
        }
    }
}