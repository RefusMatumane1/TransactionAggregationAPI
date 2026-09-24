using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.WebhookSources.Application.Persistence;
using Modules.WebhookSources.Domain.ValueObjects;
using SharedKernel.Abstractions;
using SharedKernel.Abstractions.Authentication;
using SharedKernel.Common.Models;

namespace Modules.WebhookSources.Application.Features.UpdateWebhookSourceInstitutions
{
    internal sealed class UpdateWebhookSourceInstitutionsCommandHandler(
        IWebhookSourcesDbContext context,
        IUserContext userContext,
        ILogger<UpdateWebhookSourceInstitutionsCommandHandler> logger)
        : ICommandHandler<UpdateWebhookSourceInstitutionsCommand>
    {
        public async Task<Result> Handle(UpdateWebhookSourceInstitutionsCommand request, CancellationToken cancellationToken)
        {
            var id = WebhookSourceId.CreateFrom(request.Id);
            var source = await context.WebhookSources
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

            if (source is null)
                return Result.Failure(Error.NotFound("WebhookSource", request.Id));

            source.AuthorizeInstitutions(request.AuthorizedInstitutions);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Webhook source {SourceName} ({SourceId}) authorized for institutions {Institutions} by admin {AdminId}",
                source.Name, source.Id.Value, source.AuthorizedInstitutions, userContext.UserId);

            return Result.Success();
        }
    }
}