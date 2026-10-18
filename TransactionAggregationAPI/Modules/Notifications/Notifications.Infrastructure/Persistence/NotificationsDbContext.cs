using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Modules.Audit.Contracts;
using Modules.Notifications.Application.Persistence;
using Modules.Notifications.Domain;

namespace Modules.Notifications.Infrastructure.Persistence
{
    public class NotificationsDbContext : AppDbContextBase, INotificationsDbContext
    {
        public const string Schema = "notifications";

        private readonly IAuditTrail _auditTrail;
        private readonly List<AuditEventRecord> _stagedAudit = [];

        public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options, IAuditTrail auditTrail)
            : base(options)
        {
            _auditTrail = auditTrail;
        }

        public DbSet<AlertWebhook> AlertWebhooks => Set<AlertWebhook>();

        public void StageAudit(IEnumerable<AuditEventRecord> events) => _stagedAudit.AddRange(events);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(Schema);

            modelBuilder.ApplyConfiguration(new AlertWebhookConfiguration());

            base.OnModelCreating(modelBuilder);
        }

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