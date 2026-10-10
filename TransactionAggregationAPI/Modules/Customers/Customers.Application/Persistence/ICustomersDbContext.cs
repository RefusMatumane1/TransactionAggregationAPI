using Microsoft.EntityFrameworkCore;
using Modules.Audit.Contracts;
using Modules.Customers.Domain;

namespace Modules.Customers.Application.Persistence
{
    public interface ICustomersDbContext
    {
        DbSet<Customer> Customers { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        void StageAudit(IEnumerable<AuditEventRecord> events);

        void DiscardPendingChanges();
    }
}