using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Modules.Audit.Application.Persistence;
using Modules.Audit.Domain;

namespace Modules.Audit.Infrastructure.Persistence
{
    public class AuditDbContext : AppDbContextBase, IAuditDbContext
    {
        public const string Schema = "audit";

        public AuditDbContext(DbContextOptions<AuditDbContext> options)
            : base(options)
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