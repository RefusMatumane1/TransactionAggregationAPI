using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Modules.Audit.Contracts;
using Modules.Customers.Application.Persistence;
using Modules.Customers.Domain;

namespace Modules.Customers.Infrastructure.Persistence
{
    public class CustomersDbContext : AppDbContextBase, ICustomersDbContext
    {
        // Not "customers": an earlier migration (Transactions' RemoveCustomerOwnership) drops a schema of
        // that name with CASCADE, so sharing the name would make these tables depend on migration order.
        public const string Schema = "customerdirectory";

        private readonly IAuditTrail _auditTrail;
        private readonly List<AuditEventRecord> _stagedAudit = [];

        public CustomersDbContext(DbContextOptions<CustomersDbContext> options, IAuditTrail auditTrail)
            : base(options)
        {
            _auditTrail = auditTrail;
        }

        public DbSet<Customer> Customers => Set<Customer>();

        public void StageAudit(IEnumerable<AuditEventRecord> events) => _stagedAudit.AddRange(events);

        public void DiscardPendingChanges()
        {
            ChangeTracker.Clear();
            _stagedAudit.Clear();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(Schema);

            modelBuilder.ApplyConfiguration(new CustomerConfiguration());

            base.OnModelCreating(modelBuilder);
        }

        // Every change here is administrative and audited: the change and its audit row commit together.
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_stagedAudit.Count == 0)
                return await base.SaveChangesAsync(cancellationToken);

            var audit = _stagedAudit.ToList();
            var saved = await SaveAtomicallyAsync(
                (transaction, ct) => transaction is null
                    ? _auditTrail.RecordAsync(audit, ct)
                    : _auditTrail.RecordWithinAsync(audit, transaction, ct),
                cancellationToken);

            _stagedAudit.Clear();
            return saved;
        }
    }
}