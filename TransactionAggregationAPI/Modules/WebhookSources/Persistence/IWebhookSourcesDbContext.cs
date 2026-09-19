using Microsoft.EntityFrameworkCore;

namespace Modules.WebhookSources.Persistence
{
    public interface IWebhookSourcesDbContext
    {
        DbSet<WebhookSource> WebhookSources { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
