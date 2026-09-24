using BuildingBlocks.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BuildingBlocks.Messaging
{
    public static class DependencyInjection
    {
        /// <summary>
        /// MessagingDbContext uses the same scoped NpgsqlConnection as TransactionsDbContext so the
        /// two can share one transaction: an outbox write commits atomically with the change that
        /// caused it (ADR-0009). No EnableRetryOnFailure here: a retrying strategy can't enlist in a
        /// transaction another context opened, and TransactionsDbContext already retries that one.
        /// </summary>
        public static IServiceCollection AddMessagingBuildingBlock(this IServiceCollection services)
        {
            services.AddDbContext<MessagingDbContext>((sp, options) =>
            {
                var connection = sp.GetRequiredService<NpgsqlConnection>();
                options.UseNpgsql(connection, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly("BuildingBlocks.Messaging");
                });
            });

            services.AddScoped<IMessagingDbContext>(provider =>
                provider.GetRequiredService<MessagingDbContext>());

            return services;
        }
    }
}