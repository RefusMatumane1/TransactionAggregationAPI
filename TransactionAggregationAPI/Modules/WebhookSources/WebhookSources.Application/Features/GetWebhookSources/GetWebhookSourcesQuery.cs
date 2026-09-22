using SharedKernel.Abstractions;
using Modules.WebhookSources.Application.DTOs;

namespace Modules.WebhookSources.Application.Features.GetWebhookSources
{
    public sealed record GetWebhookSourcesQuery : IQuery<IReadOnlyList<WebhookSourceDto>>;
}
