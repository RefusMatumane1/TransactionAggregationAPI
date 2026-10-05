using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Modules.Audit.Domain;

namespace Modules.Audit.Application.Persistence
{
    public interface IAuditDbContext
    {
        DbSet<AuditEvent> AuditEvents { get; }

        DatabaseFacade Database { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}