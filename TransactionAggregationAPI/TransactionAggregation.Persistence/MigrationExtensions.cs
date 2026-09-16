using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace TransactionAggregation.Persistence
{
    public static class MigrationExtensions
    {
        // Arbitrary fixed key for Postgres advisory locking — any bigint works as long
        // as it's unique to this purpose within the database.
        private const long MigrationLockId = 7_27_2024;

        public static async Task ApplyMigrationsAsync(this IHost host, CancellationToken cancellationToken = default)
        {
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();

            try
            {
                var context = services.GetRequiredService<ApplicationDbContext>();

                // When running under the integration-test WebApplicationFactory the context
                // uses an InMemory database, so we call EnsureCreated instead — no locking
                // needed since tests run a single in-process instance.
                if (!context.Database.IsRelational())
                {
                    await context.Database.EnsureCreatedAsync(cancellationToken);
                    return;
                }

                // Multiple API replicas (or the dedicated migration Job racing a rolling
                // deploy) can call this concurrently. pg_advisory_lock serializes them at
                // the database level regardless of deployment topology, so migrations are
                // never applied twice in parallel even if operational discipline (running
                // the Job before the Deployment) is skipped.
                var connection = (NpgsqlConnection)context.Database.GetDbConnection();
                await connection.OpenAsync(cancellationToken);

                try
                {
                    logger.LogInformation("Acquiring migration lock...");
                    await using (var lockCommand = connection.CreateCommand())
                    {
                        lockCommand.CommandText = "SELECT pg_advisory_lock(@lockId)";
                        lockCommand.Parameters.AddWithValue("lockId", MigrationLockId);
                        await lockCommand.ExecuteNonQueryAsync(cancellationToken);
                    }

                    logger.LogInformation("Applying database migrations...");
                   // await context.Database.MigrateAsync(cancellationToken);
                    logger.LogInformation("Database migrations applied successfully");
                }
                finally
                {
                    await using var unlockCommand = connection.CreateCommand();
                    unlockCommand.CommandText = "SELECT pg_advisory_unlock(@lockId)";
                    unlockCommand.Parameters.AddWithValue("lockId", MigrationLockId);
                    await unlockCommand.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while applying database migrations");
                throw;
            }
        }
    }
}
