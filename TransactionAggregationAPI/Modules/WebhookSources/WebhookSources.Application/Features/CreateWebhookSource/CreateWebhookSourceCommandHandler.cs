using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Abstractions.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.Audit.Contracts;
using Modules.WebhookSources.Application.Common;
using Modules.WebhookSources.Application.Persistence;
using Modules.WebhookSources.Domain;
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
            var code = request.Code.Trim();
            var codeExists = await context.WebhookSources
                .AnyAsync(s => s.Name.ToLower() == code.ToLower(), cancellationToken);

            if (codeExists)
                return Result.Failure<CreateWebhookSourceResult>(
                    Error.Conflict($"A bank with code '{code}' already exists."));

            var (source, apiKey) = WebhookSource.Create(code, request.DisplayName, request.Color);

            context.WebhookSources.Add(source);
            context.StageAudit([AdminAudit.Of(AuditEventTypes.SourceCreated, source, userContext.UserId, $"Bank '{source.Name}' created with a new webhook API key")]);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Bank source {SourceName} ({SourceId}) created by admin {AdminId}",
                source.Name, source.Id.Value, userContext.UserId);

            return Result.Success(new CreateWebhookSourceResult(
                source.Id.Value, source.Name, source.DisplayName, source.Color, apiKey));
        }
    }
}