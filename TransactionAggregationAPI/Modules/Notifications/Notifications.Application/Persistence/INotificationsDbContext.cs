using Microsoft.EntityFrameworkCore;
using Modules.Audit.Contracts;
using Modules.Notifications.Domain;

namespace Modules.Notifications.Application.Persistence
{
    public interface INotificationsDbContext
    {
        DbSet<AlertWebhook> AlertWebhooks { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        void StageAudit(IEnumerable<AuditEventRecord> events);
    }
}