using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Customers.Application.Persistence;
using Modules.Customers.Domain;
using SharedKernel.Persistence;

namespace Modules.Customers.Infrastructure.Persistence
{
    /// <summary>Owns the "customers" schema and its own migrations history (ADR-0009).</summary>
    public class CustomersDbContext : AppDbContextBase, ICustomersDbContext
    {
        public CustomersDbContext(DbContextOptions<CustomersDbContext> options, IMediator mediator)
            : base(options, mediator)
        {
        }

        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Account> Accounts => Set<Account>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("customers");

            modelBuilder.ApplyConfiguration(new CustomerConfiguration());
            modelBuilder.ApplyConfiguration(new AccountConfiguration());

            base.OnModelCreating(modelBuilder);
        }
    }
}