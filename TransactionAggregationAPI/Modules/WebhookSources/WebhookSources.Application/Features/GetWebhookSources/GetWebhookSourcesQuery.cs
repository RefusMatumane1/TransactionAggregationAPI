using Modules.WebhookSources.Application.DTOs;
using SharedKernel.Abstractions;

namespace Modules.WebhookSources.Application.Features.GetWebhookSources
{
    public sealed record GetWebhookSourcesQuery : IQuery<IReadOnlyList<WebhookSourceDto>>;
}