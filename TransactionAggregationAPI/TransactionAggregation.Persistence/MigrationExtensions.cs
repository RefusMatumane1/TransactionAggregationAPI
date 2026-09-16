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

        public static async Task ApplyMigrationsAsync(this IHost host, CancellationToken cancellationToken = default)
        {
            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<ApplicationDbContext>>();

            try
            {
                var context = services.GetRequiredService<ApplicationDbContext>();

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

                    logger.LogInformation("Applying database migrations...");

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