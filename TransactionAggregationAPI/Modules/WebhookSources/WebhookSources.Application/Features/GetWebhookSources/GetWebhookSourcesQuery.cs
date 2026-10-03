using BuildingBlocks.Application.Abstractions;
using BuildingBlocks.Application.Pagination;
using FluentValidation;
using Modules.WebhookSources.Application.DTOs;
using Modules.WebhookSources.Domain;
using Modules.WebhookSources.Domain.ValueObjects;

namespace Modules.WebhookSources.Application.Features.GetWebhookSources
{
    public sealed record GetWebhookSourcesQuery(string? Cursor = null, int PageSize = GetWebhookSourcesQuery.DefaultPageSize)
        : IQuery<CursorPage<WebhookSourceDto>>
    {
        public const int DefaultPageSize = 50;
        public const int MaxPageSize = 100;
    }

    public static class WebhookSourceSort
    {
        public static readonly KeysetSort<WebhookSource> ByName =
            new KeysetSort<WebhookSource, string, WebhookSourceId>(
                "name", s => s.Name, s => s.Id, id => id.Value, WebhookSourceId.CreateFrom, KeyCodecs.String);
    }

    public sealed class GetWebhookSourcesQueryValidator : AbstractValidator<GetWebhookSourcesQuery>
    {
        public GetWebhookSourcesQueryValidator()
        {
            RuleFor(x => x.PageSize).InclusiveBetween(1, GetWebhookSourcesQuery.MaxPageSize);
            RuleFor(x => x.Cursor).MustBeACursorFor(WebhookSourceSort.ByName, descending: false);
        }
    }
}