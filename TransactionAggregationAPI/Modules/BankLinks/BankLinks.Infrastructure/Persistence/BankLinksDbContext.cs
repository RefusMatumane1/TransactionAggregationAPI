using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.BankLinks.Application.Persistence;
using Modules.BankLinks.Domain;
using SharedKernel.Persistence;

namespace Modules.BankLinks.Infrastructure.Persistence
{
    /// <summary>
    /// Owns its own "banklinks" Postgres schema and its own EF Core migrations history,
    /// independent of every other module's DbContext — see
    /// docs/adr/0009-schema-per-module-database-strategy.md. No cross-context
    /// transaction sharing with TransactionsDbContext: activating a link (writing here)
    /// and provisioning its Account (writing to TransactionsDbContext via
    /// IAccountProvisioningPort) are two separate units of work, same trade-off already
    /// accepted for WebhookSourcesDbContext.
    /// </summary>
    public class BankLinksDbContext : AppDbContextBase, IBankLinksDbContext
    {
        public BankLinksDbContext(DbContextOptions<BankLinksDbContext> options, IMediator mediator)
            : base(options, mediator)
        {
        }

        public DbSet<BankLink> BankLinks => Set<BankLink>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("banklinks");

            modelBuilder.ApplyConfiguration(new BankLinkConfiguration());

            base.OnModelCreating(modelBuilder);
        }
    }
}