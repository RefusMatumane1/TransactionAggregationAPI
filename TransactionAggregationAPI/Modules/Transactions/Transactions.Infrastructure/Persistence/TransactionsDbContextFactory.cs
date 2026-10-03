using BuildingBlocks.Messaging.Persistence;
using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Modules.Audit.Contracts;

namespace Modules.Transactions.Infrastructure.Persistence
{
    public class TransactionsDbContextFactory : IDesignTimeDbContextFactory<TransactionsDbContext>
    {
        public TransactionsDbContext CreateDbContext(string[] args)
        {
            var connectionString = DesignTime.ConnectionString;

            var optionsBuilder = new DbContextOptionsBuilder<TransactionsDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            var messagingOptionsBuilder = new DbContextOptionsBuilder<MessagingDbContext>();
            messagingOptionsBuilder.UseNpgsql(connectionString);
            var messagingDbContext = new MessagingDbContext(messagingOptionsBuilder.Options);

            return new TransactionsDbContext(optionsBuilder.Options, messagingDbContext, new UnavailableAuditTrail());
        }
    }
}