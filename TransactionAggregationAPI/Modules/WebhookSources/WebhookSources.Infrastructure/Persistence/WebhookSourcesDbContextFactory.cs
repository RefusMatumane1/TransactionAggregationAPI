using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SharedKernel.Persistence;

namespace Modules.WebhookSources.Infrastructure.Persistence;

public class WebhookSourcesDbContextFactory : IDesignTimeDbContextFactory<WebhookSourcesDbContext>
{
    public WebhookSourcesDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WebhookSourcesDbContext>();
        optionsBuilder.UseNpgsql(DesignTime.ConnectionString);

        return new WebhookSourcesDbContext(optionsBuilder.Options, DesignTime.NoOpMediator);
    }
}