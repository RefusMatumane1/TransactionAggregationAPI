using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.BankLinks.Application.Persistence;
using Modules.BankLinks.Domain;
using SharedKernel.Persistence;

namespace Modules.BankLinks.Infrastructure.Persistence
{
    /// <summary>Owns the "banklinks" schema and its own migrations history (ADR-0009).</summary>
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