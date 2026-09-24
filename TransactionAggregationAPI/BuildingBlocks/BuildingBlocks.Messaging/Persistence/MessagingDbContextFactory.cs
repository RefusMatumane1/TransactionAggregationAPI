using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SharedKernel.Persistence;

namespace BuildingBlocks.Messaging.Persistence;

public class MessagingDbContextFactory : IDesignTimeDbContextFactory<MessagingDbContext>
{
    public MessagingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MessagingDbContext>();
        optionsBuilder.UseNpgsql(DesignTime.ConnectionString);

        return new MessagingDbContext(optionsBuilder.Options, DesignTime.NoOpMediator);
    }
}