using Microsoft.EntityFrameworkCore;
using Modules.Audit.Domain;

namespace Modules.Audit.Application.Persistence
{
    public interface IAuditDbContext
    {
        DbSet<AuditEvent> AuditEvents { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}