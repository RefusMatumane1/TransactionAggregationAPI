using SharedKernel.Abstractions;
using Modules.WebhookSources.DTOs;

namespace Modules.WebhookSources.Features.GetWebhookSources
{
    public sealed record GetWebhookSourcesQuery : IQuery<IReadOnlyList<WebhookSourceDto>>;
}
