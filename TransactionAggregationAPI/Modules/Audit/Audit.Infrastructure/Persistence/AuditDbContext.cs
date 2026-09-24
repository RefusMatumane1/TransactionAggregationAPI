using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Audit.Application.Persistence;
using Modules.Audit.Domain;
using SharedKernel.Persistence;

namespace Modules.Audit.Infrastructure.Persistence
{
    /// <summary>
    /// Owns the "audit" Postgres schema and its own migrations history (ADR-0009). It uses
    /// its own connection rather than the shared scoped one: audit rows are written either
    /// by the outbox dispatcher (after the producing transaction committed) or directly for
    /// facts with no database change, so it never needs to join another module's transaction.
    /// See docs/adr/0011-audit-trail-for-inbound-data.md.
    /// </summary>
    public class AuditDbContext : AppDbContextBase, IAuditDbContext
    {
        public const string Schema = "audit";

        public AuditDbContext(DbContextOptions<AuditDbContext> options, IMediator mediator)
            : base(options, mediator)
        {
        }

        public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(Schema);

            modelBuilder.ApplyConfiguration(new AuditEventConfiguration());

            base.OnModelCreating(modelBuilder);
        }
    }
}