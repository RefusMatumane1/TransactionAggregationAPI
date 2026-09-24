using Microsoft.EntityFrameworkCore;
using Modules.WebhookSources.Application.Persistence;
using Modules.WebhookSources.Contracts;

namespace Modules.WebhookSources.Application.Contracts
{
    /// <summary>
    /// Read-only, unlike <see cref="WebhookSourceAuthenticator"/>: it's called once per Kafka
    /// record, and stamping LastUsedAt on every record would turn a lookup into a write.
    /// </summary>
    internal sealed class WebhookSourceDirectory(IWebhookSourcesDbContext context) : IWebhookSourceDirectory
    {
        public Task<bool> IsActiveAsync(string sourceName, CancellationToken cancellationToken = default) =>
            context.WebhookSources
                .AsNoTracking()
                .AnyAsync(s => s.Name == sourceName && s.IsActive, cancellationToken);

        public async Task<IReadOnlySet<string>> GetAuthorizedInstitutionsAsync(
            string sourceName, CancellationToken cancellationToken = default)
        {
            var institutions = await context.WebhookSources
                .AsNoTracking()
                .Where(s => s.Name == sourceName && s.IsActive)
                .Select(s => s.AuthorizedInstitutions)
                .FirstOrDefaultAsync(cancellationToken);

            return new HashSet<string>(institutions ?? [], StringComparer.OrdinalIgnoreCase);
        }
    }
}