using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace TransactionAggregation.Persistence
{
    public static class MigrationExtensions
    {

        private const long MigrationLockId = 7_27_2024;

        /// <summary>
        /// Generic over TContext so every module's own DbContext (each with its own
        /// schema/migration history — see docs/adr/0009-schema-per-module-database-strategy.md)
        /// can apply its migrations the same way, called once per context from
        /// Program.cs. The shared MigrationLockId is safe to reuse across contexts:
        /// they migrate sequentially within one process, and the lock exists to guard
        /// against multiple pod replicas racing to migrate concurrently, not against
        /// these calls racing each other.
        /// </summary>
        public static async Task ApplyMigrationsAsync<TContext>(this IHost host, CancellationToken cancellationToken = default)
            where TContext : DbContext
        {
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<TContext>>();

            try
            {
                var context = services.GetRequiredService<TContext>();

                if (!context.Database.IsRelational())
                {
                    await context.Database.EnsureCreatedAsync(cancellationToken);
                    return;
                }

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

                    logger.LogInformation("Applying database migrations for {ContextType}...", typeof(TContext).Name);

                    await context.Database.MigrateAsync(cancellationToken);

                    logger.LogInformation("Database migrations applied successfully for {ContextType}", typeof(TContext).Name);
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
                logger.LogError(ex, "An error occurred while applying database migrations for {ContextType}", typeof(TContext).Name);
                throw;
            }
        }
    }
}
