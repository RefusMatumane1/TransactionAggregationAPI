using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SharedKernel.Persistence;

namespace Modules.BankLinks.Infrastructure.Persistence;

public class BankLinksDbContextFactory : IDesignTimeDbContextFactory<BankLinksDbContext>
{
    public BankLinksDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BankLinksDbContext>();
        optionsBuilder.UseNpgsql(DesignTime.ConnectionString);

        return new BankLinksDbContext(optionsBuilder.Options, DesignTime.NoOpMediator);
    }
}