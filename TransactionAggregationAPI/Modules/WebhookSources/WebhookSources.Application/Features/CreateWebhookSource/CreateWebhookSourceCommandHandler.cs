using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.WebhookSources.Application.Persistence;
using Modules.WebhookSources.Domain;
using SharedKernel.Abstractions;
using SharedKernel.Abstractions.Authentication;
using SharedKernel.Common.Models;

namespace Modules.WebhookSources.Application.Features.CreateWebhookSource
{
    internal sealed class CreateWebhookSourceCommandHandler(
        IWebhookSourcesDbContext context,
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

            var (source, apiKey) = WebhookSource.Create(request.Name, request.AuthorizedInstitutions);

            await context.WebhookSources.AddAsync(source, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Webhook source {SourceName} ({SourceId}) created by admin {AdminId} for institutions {Institutions}",
                source.Name, source.Id.Value, userContext.UserId, source.AuthorizedInstitutions);

            return Result.Success(new CreateWebhookSourceResult(
                source.Id.Value, source.Name, apiKey, source.AuthorizedInstitutions));
        }
    }
}