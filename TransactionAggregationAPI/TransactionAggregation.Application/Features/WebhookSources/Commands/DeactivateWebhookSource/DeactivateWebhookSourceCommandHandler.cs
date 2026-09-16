using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Abstractions.Authentication;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Application.Features.WebhookSources.Commands.DeactivateWebhookSource
{
    internal sealed class DeactivateWebhookSourceCommandHandler(
        IApplicationDbContext context,
        IUserContext userContext,
        ILogger<DeactivateWebhookSourceCommandHandler> logger)
        : ICommandHandler<DeactivateWebhookSourceCommand>
    {
        public async Task<Result> Handle(DeactivateWebhookSourceCommand request, CancellationToken cancellationToken)
        {
            try
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
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deactivating webhook source {WebhookSourceId}", request.Id);
                return Result.Failure(Error.Unexpected);
            }
        }
    }
}