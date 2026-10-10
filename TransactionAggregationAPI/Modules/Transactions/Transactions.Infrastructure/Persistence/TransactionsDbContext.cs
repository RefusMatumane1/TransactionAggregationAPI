using BuildingBlocks.Messaging.Outbox;
using BuildingBlocks.Messaging.Persistence;
using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Modules.Audit.Contracts;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Infrastructure.Persistence.Configurations;

namespace Modules.Transactions.Infrastructure.Persistence
{
    public class TransactionsDbContext : AppDbContextBase, ITransactionsDbContext
    {
        private readonly MessagingDbContext _messaging;
        private readonly IAuditTrail _auditTrail;
        private readonly List<AuditEventRecord> _stagedAudit = [];

        public TransactionsDbContext(
            DbContextOptions<TransactionsDbContext> options,
            MessagingDbContext messaging,
            IAuditTrail auditTrail)
            : base(options)
        {
            _messaging = messaging;
            _auditTrail = auditTrail;
        }

        public DbSet<Transaction> Transactions => Set<Transaction>();

        public DbSet<DailyTotal> DailyTotals => Set<DailyTotal>();

        public DbSet<AggregationCheckpoint> AggregationCheckpoints => Set<AggregationCheckpoint>();

        public void StageAudit(IEnumerable<AuditEventRecord> events) => _stagedAudit.AddRange(events);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("transactions");

            modelBuilder.ApplyConfiguration(new TransactionConfiguration());
            modelBuilder.ApplyConfiguration(new DailyTotalConfiguration());
            modelBuilder.ApplyConfiguration(new AggregationCheckpointConfiguration());

            base.OnModelCreating(modelBuilder);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var audit = _stagedAudit.ToList();
            if (audit.Count == 0 && !ChangeTracker.HasChanges() && !_messaging.ChangeTracker.HasChanges())
                return 0;

            var saved = await SaveAtomicallyAsync(async (transaction, ct) =>
            {
                if (transaction is null)
                {
                    await _messaging.SaveChangesAsync(acceptAllChangesOnSuccess: false, ct);
                    if (audit.Count > 0)
                        await _auditTrail.RecordAsync(audit, ct);
                    return;
                }

                await _messaging.Database.UseTransactionAsync(transaction, ct);
                try
                {
                    await _messaging.SaveChangesAsync(acceptAllChangesOnSuccess: false, ct);
                    if (audit.Count > 0)
                        await _auditTrail.RecordWithinAsync(audit, transaction, ct);
                }
                finally
                {
                    await _messaging.Database.UseTransactionAsync(null, CancellationToken.None);
                }
            }, cancellationToken);

            _messaging.ChangeTracker.AcceptAllChanges();
            _stagedAudit.Clear();
            return saved;
        }

        public void DiscardPendingChanges()
        {
            ChangeTracker.Clear();
            _stagedAudit.Clear();

            foreach (var entry in _messaging.ChangeTracker.Entries<OutboxMessage>()
                         .Where(e => e.State == EntityState.Added)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
    }
}