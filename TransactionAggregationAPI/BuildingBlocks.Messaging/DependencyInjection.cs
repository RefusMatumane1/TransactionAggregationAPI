using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using BuildingBlocks.Messaging.Persistence;

namespace BuildingBlocks.Messaging
{
    public static class DependencyInjection
    {
        /// <summary>
        /// MessagingDbContext is registered against the SAME scoped NpgsqlConnection as
        /// ApplicationDbContext (registered once in Program.cs) rather than its own
        /// connection string/connection — required for the two contexts to share a
        /// transaction (Database.UseTransactionAsync) so an Outbox write commits
        /// atomically with the business-entity write that triggered it. See
        /// docs/adr/0009-schema-per-module-database-strategy.md.
        ///
        /// No EnableRetryOnFailure here deliberately: a retrying execution strategy
        /// forbids a context from participating in a transaction it didn't open itself
        /// via CreateExecutionStrategy().ExecuteAsync, which ApplicationDbContext's
        /// SaveChangesAsync already does for the shared transaction — adding a second,
        /// independent retrying strategy on this context risks exactly that guard
        /// throwing when it's writing under a transaction ApplicationDbContext owns.
        /// The dispatcher background services (which use this context standalone, with
        /// no shared transaction) simply don't get automatic retry on transient
        /// connection failures as a result — a deliberate, narrower trade-off, not an
        /// oversight.
        /// </summary>
        public static IServiceCollection AddMessagingBuildingBlock(
            this IServiceCollection services,
            IConfiguration configuration)
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
