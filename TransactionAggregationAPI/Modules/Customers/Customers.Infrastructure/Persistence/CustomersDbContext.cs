using MediatR;
using Microsoft.EntityFrameworkCore;
using Modules.Customers.Application.Persistence;
using Modules.Customers.Domain;
using SharedKernel.Persistence;

namespace Modules.Customers.Infrastructure.Persistence
{
    /// <summary>
    /// Owns its own "customers" Postgres schema and its own EF Core migrations history,
    /// independent of every other module's DbContext — see
    /// docs/adr/0009-schema-per-module-database-strategy.md. No cross-context
    /// transaction sharing with TransactionsDbContext: creating a Customer/Account here
    /// and writing a Transaction against it (the Transactions module's
    /// TransactionsDbContext, read back via IAccountBalanceProvider) are two separate units of work, same trade-off already
    /// accepted for WebhookSourcesDbContext/BankLinksDbContext.
    /// </summary>
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