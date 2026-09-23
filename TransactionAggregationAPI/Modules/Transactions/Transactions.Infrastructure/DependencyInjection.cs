using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Modules.Customers.Contracts;
using Modules.Transactions.Application.Adapters;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Application.Common.Options;
using Modules.Transactions.Infrastructure.BackgroundServices;
using Modules.Transactions.Infrastructure.Persistence;
using Modules.Transactions.Infrastructure.Services;

namespace Modules.Transactions
{
    public static class TransactionsInfrastructureDependencyInjection
    {
        /// <summary>
        /// TransactionsDbContext is built on the host's scoped NpgsqlConnection — the same
        /// one MessagingDbContext uses — so its SaveChangesAsync can commit Outbox writes in
        /// one transaction with the Transaction rows (ADR-0003). The host must register that
        /// connection before this runs.
        /// </summary>
        public static IServiceCollection AddTransactionsModule(
            this IServiceCollection services,
            IConfiguration configuration,
            bool isDevelopment = false)
        {
            services.AddTransactionsApplication(configuration);

            services.AddDbContext<TransactionsDbContext>((sp, options) =>
            {
                options.UseNpgsql(sp.GetRequiredService<NpgsqlConnection>(), npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly("Transactions.Infrastructure");
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                });

                if (isDevelopment)
                {
                    options.EnableDetailedErrors();
                    options.EnableSensitiveDataLogging();
                }
            });

            services.AddScoped<ITransactionsDbContext>(provider =>
                provider.GetRequiredService<TransactionsDbContext>());

            // Implements the port Customers owns — see docs/adr/0010-consumer-owned-ports-for-unextracted-dependencies.md.
            services.AddScoped<IAccountBalanceProvider, TransactionBalanceProvider>();

            services.AddHttpClient();

            services.Configure<NotificationOptions>(
                configuration.GetSection("NotificationOptions"));
            services.AddScoped<INotificationService, NotificationService>();

            services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
            services.AddHostedService<OutboxDispatcherBackgroundService>();

            services.Configure<InboxOptions>(configuration.GetSection(InboxOptions.SectionName));
            services.AddHostedService<InboxDispatcherBackgroundService>();

            return services;
        }
    }
}
