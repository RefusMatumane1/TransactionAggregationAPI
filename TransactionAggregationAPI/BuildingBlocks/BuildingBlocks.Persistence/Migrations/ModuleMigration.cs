using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BuildingBlocks.Persistence.Migrations
{
    internal sealed class ModuleMigration<TContext> : IModuleMigration
        where TContext : DbContext
    {
        // Serialises migrators across replicas and across modules: one schema change at a time.
        private const long MigrationLockId = 7_27_2024;

        public string Name => typeof(TContext).Name;

        public async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            using var scope = services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<TContext>>();

            try
            {
                var context = scope.ServiceProvider.GetRequiredService<TContext>();

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
                    await SetLockAsync(connection, "pg_advisory_lock", cancellationToken);

                    logger.LogInformation("Applying database migrations for {ContextType}...", Name);
                    await context.Database.MigrateAsync(cancellationToken);
                    logger.LogInformation("Database migrations applied successfully for {ContextType}", Name);
                }
                finally
                {
                    await SetLockAsync(connection, "pg_advisory_unlock", CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while applying database migrations for {ContextType}", Name);
                throw;
            }
        }

        private static async Task SetLockAsync(NpgsqlConnection connection, string function, CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {function}(@lockId)";
            command.Parameters.AddWithValue("lockId", MigrationLockId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
