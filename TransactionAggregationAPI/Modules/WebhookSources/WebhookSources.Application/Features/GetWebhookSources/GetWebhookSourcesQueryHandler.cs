using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Pagination;
using Microsoft.EntityFrameworkCore;
using Modules.WebhookSources.Application.DTOs;
using Modules.WebhookSources.Application.Persistence;
using SharedKernel.Common.Models;

namespace Modules.WebhookSources.Application.Features.GetWebhookSources
{
    internal sealed class GetWebhookSourcesQueryHandler(IWebhookSourcesDbContext context, IKeysetPaginator paginator)
        : IQueryHandler<GetWebhookSourcesQuery, CursorPage<WebhookSourceDto>>
    {
        public async Task<Result<CursorPage<WebhookSourceDto>>> Handle(GetWebhookSourcesQuery request, CancellationToken cancellationToken)
        {
            var query = context.WebhookSources.AsNoTracking();

            var sort = WebhookSourceSort.ByName;
            var window = PageCursor.TryDecode(request.Cursor, out var cursor)
                ? sort.After(query, paginator, cursor!)
                : query;

            var sources = await sort.Order(window, descending: false)
                .Take(request.PageSize + 1)
                .ToListAsync(cancellationToken);

            return Result.Success(sort.ToPage(sources, request.PageSize, descending: false, s => new WebhookSourceDto(
                s.Id.Value, s.Name, s.DisplayName, s.Color, s.IsActive, s.CreatedAt, s.LastUsedAt, s.SigningPublicKey != null)));
        }
    }
}