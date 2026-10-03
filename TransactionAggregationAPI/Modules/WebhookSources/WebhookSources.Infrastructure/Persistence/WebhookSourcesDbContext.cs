using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Modules.Audit.Contracts;
using Modules.WebhookSources.Application.Persistence;
using Modules.WebhookSources.Domain;

namespace Modules.WebhookSources.Infrastructure.Persistence
{
    public class WebhookSourcesDbContext : AppDbContextBase, IWebhookSourcesDbContext
    {
        private readonly IAuditTrail _auditTrail;
        private readonly List<AuditEventRecord> _stagedAudit = [];

        public WebhookSourcesDbContext(DbContextOptions<WebhookSourcesDbContext> options, IAuditTrail auditTrail)
            : base(options)
        {
            _auditTrail = auditTrail;
        }

        public DbSet<WebhookSource> WebhookSources => Set<WebhookSource>();

        public void StageAudit(IEnumerable<AuditEventRecord> events) => _stagedAudit.AddRange(events);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("webhooksources");

            modelBuilder.ApplyConfiguration(new WebhookSourceConfiguration());

            base.OnModelCreating(modelBuilder);
        }

        // An administrative change and its audit row commit together; a save with nothing to audit
        // (the throttled LastUsedAt write on authentication) needs no explicit transaction.
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