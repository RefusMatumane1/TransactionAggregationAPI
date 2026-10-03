using Microsoft.EntityFrameworkCore;
using Modules.Audit.Contracts;
using Modules.WebhookSources.Domain;

namespace Modules.WebhookSources.Application.Persistence
{
    public interface IWebhookSourcesDbContext
    {
        DbSet<WebhookSource> WebhookSources { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        // Staged events are written inside the next SaveChangesAsync's database transaction.
        void StageAudit(IEnumerable<AuditEventRecord> events);
    }
}