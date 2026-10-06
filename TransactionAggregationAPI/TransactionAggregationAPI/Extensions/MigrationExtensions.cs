using BuildingBlocks.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Modules.Audit.Infrastructure.Persistence;
using Modules.Customers.Infrastructure.Persistence;
using Modules.Transactions.Infrastructure.Persistence;
using Modules.WebhookSources.Infrastructure.Persistence;
using Npgsql;

namespace TransactionAggregationAPI.Extensions
{
    public static class MigrationExtensions
    {
        public static async Task ApplyAllModuleMigrationsAsync(this IHost host)
        {
            await host.ApplyMigrationsAsync<TransactionsDbContext>();
            await host.ApplyMigrationsAsync<MessagingDbContext>();
            await host.ApplyMigrationsAsync<WebhookSourcesDbContext>();
            await host.ApplyMigrationsAsync<AuditDbContext>();
            await host.ApplyMigrationsAsync<CustomersDbContext>();
        }

        private const long MigrationLockId = 7_27_2024;

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