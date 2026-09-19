using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstractions;
using SharedKernel.Common.Models;
using Modules.WebhookSources.DTOs;
using Modules.WebhookSources.Persistence;

namespace Modules.WebhookSources.Features.GetWebhookSources
{
    internal sealed class GetWebhookSourcesQueryHandler(IWebhookSourcesDbContext context)
        : IQueryHandler<GetWebhookSourcesQuery, IReadOnlyList<WebhookSourceDto>>
    {
        public async Task<Result<IReadOnlyList<WebhookSourceDto>>> Handle(GetWebhookSourcesQuery request, CancellationToken cancellationToken)
        {
            var sources = await context.WebhookSources
                .AsNoTracking()
                .OrderBy(s => s.Name)
                .ToListAsync(cancellationToken);

            var dtos = sources
                            .Select(s => new WebhookSourceDto(s.Id.Value, s.Name, s.IsActive, s.CreatedAt, s.LastUsedAt))
                            .ToList();

            return Result.Success<IReadOnlyList<WebhookSourceDto>>(dtos);
        }
    }
}
