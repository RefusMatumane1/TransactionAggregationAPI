using BuildingBlocks.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SharedKernel.Persistence;

namespace Modules.Transactions.Infrastructure.Persistence;

public class TransactionsDbContextFactory : IDesignTimeDbContextFactory<TransactionsDbContext>
{
    public TransactionsDbContext CreateDbContext(string[] args)
    {
        var connectionString = DesignTime.ConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<TransactionsDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        var messagingOptionsBuilder = new DbContextOptionsBuilder<MessagingDbContext>();
        messagingOptionsBuilder.UseNpgsql(connectionString);
        var messagingDbContext = new MessagingDbContext(messagingOptionsBuilder.Options, DesignTime.NoOpMediator);

        return new TransactionsDbContext(optionsBuilder.Options, DesignTime.NoOpMediator, messagingDbContext);
    }
}