using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


namespace TransactionAggregation.Persistence
{
    public static class MigrationExtensions
    {
        public static async Task ApplyMigrationsAsync(this IHost host, CancellationToken cancellationToken = default)
        {
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();

            try
            {
                var context = services.GetRequiredService<ApplicationDbContext>();
                logger.LogInformation("Applying database migrations...");

                // MigrateAsync is only available for relational providers.
                // When running under the integration-test WebApplicationFactory the context
                // uses an InMemory database, so we call EnsureCreated instead.
                if (context.Database.IsRelational())
                    await context.Database.MigrateAsync(cancellationToken);
                else
                    await context.Database.EnsureCreatedAsync(cancellationToken);

                logger.LogInformation("Database migrations applied successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while applying database migrations");
                throw;
            }
        }
    }
}
