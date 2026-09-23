using Microsoft.EntityFrameworkCore;
using Modules.WebhookSources.Application.Persistence;
using Modules.WebhookSources.Contracts;
using Modules.WebhookSources.Domain;

namespace Modules.WebhookSources.Application.Contracts
{
    internal sealed class WebhookSourceAuthenticator(IWebhookSourcesDbContext context) : IWebhookSourceAuthenticator
    {
        public async Task<string?> AuthenticateAsync(string apiKey, CancellationToken cancellationToken = default)
        {
            var keyHash = WebhookSource.HashKey(apiKey);

            var source = await context.WebhookSources
                .FirstOrDefaultAsync(s => s.KeyHash == keyHash && s.IsActive, cancellationToken);

            if (source is null)
                return null;

            source.RecordUsage();
            await context.SaveChangesAsync(cancellationToken);

            return source.Name;
        }
    }
}
