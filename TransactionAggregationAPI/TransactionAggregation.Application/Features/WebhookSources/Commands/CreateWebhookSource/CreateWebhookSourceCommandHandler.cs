using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Abstractions.Authentication;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;

namespace TransactionAggregation.Application.Features.WebhookSources.Commands.CreateWebhookSource
{
    internal sealed class CreateWebhookSourceCommandHandler(
        IApplicationDbContext context,
        IUserContext userContext,
        ILogger<CreateWebhookSourceCommandHandler> logger)
        : ICommandHandler<CreateWebhookSourceCommand, CreateWebhookSourceResult>
    {
        public async Task<Result<CreateWebhookSourceResult>> Handle(CreateWebhookSourceCommand request, CancellationToken cancellationToken)
        {
            var nameExists = await context.WebhookSources
                .AnyAsync(s => s.Name == request.Name, cancellationToken);

            if (nameExists)
                return Result.Failure<CreateWebhookSourceResult>(
                    Error.Conflict($"A webhook source named '{request.Name}' already exists."));

            var (source, apiKey) = Domain.Entities.WebhookSource.Create(request.Name);

            await context.WebhookSources.AddAsync(source, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Webhook source {SourceName} ({SourceId}) created by admin {AdminId}",
                source.Name, source.Id.Value, userContext.UserId);

            return Result.Success(new CreateWebhookSourceResult(source.Id.Value, source.Name, apiKey));
        }
    }
}