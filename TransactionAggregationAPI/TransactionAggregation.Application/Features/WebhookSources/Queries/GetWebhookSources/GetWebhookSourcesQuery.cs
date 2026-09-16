using TransactionAggregation.Application.Abstractions;
using TransactionAggregation.Application.Common.DTOs;

namespace TransactionAggregation.Application.Features.WebhookSources.Queries.GetWebhookSources
{
    public sealed record GetWebhookSourcesQuery : IQuery<IReadOnlyList<WebhookSourceDto>>;
}