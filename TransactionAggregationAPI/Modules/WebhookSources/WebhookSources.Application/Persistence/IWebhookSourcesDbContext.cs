using Microsoft.EntityFrameworkCore;
using Modules.WebhookSources.Domain;

namespace Modules.WebhookSources.Application.Persistence
{
    public interface IWebhookSourcesDbContext
    {
        DbSet<WebhookSource> WebhookSources { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}