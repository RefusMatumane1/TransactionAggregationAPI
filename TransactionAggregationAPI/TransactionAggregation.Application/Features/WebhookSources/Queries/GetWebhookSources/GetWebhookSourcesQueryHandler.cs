using Microsoft.EntityFrameworkCore;
using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;

namespace TransactionAggregation.Application.Features.WebhookSources.Queries.GetWebhookSources
{
    internal sealed class GetWebhookSourcesQueryHandler(IApplicationDbContext context)
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