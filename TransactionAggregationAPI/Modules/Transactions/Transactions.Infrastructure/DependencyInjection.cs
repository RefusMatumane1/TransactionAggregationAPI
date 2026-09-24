using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Customers.Contracts;
using Modules.Transactions.Application.Adapters;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Infrastructure.BackgroundServices;
using Modules.Transactions.Infrastructure.Kafka;
using Modules.Transactions.Infrastructure.Persistence;
using Modules.Transactions.Infrastructure.Services;
using Npgsql;

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

            return services;
        }

        /// <summary>
        /// The module's background processing — the Kafka consumer and the inbox, outbox and
        /// pending-expiry dispatchers. Registered only by the worker host
        /// (TransactionAggregation.Worker), so the API's replicas serve HTTP only and the two
        /// scale independently. Every dispatcher claims work through Postgres row claims, so any
        /// number of worker replicas is safe. Requires <see cref="AddTransactionsModule"/>.
        /// </summary>
        public static IServiceCollection AddTransactionsBackgroundProcessing(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
            services.AddHostedService<OutboxDispatcherBackgroundService>();

            services.Configure<InboxOptions>(configuration.GetSection(InboxOptions.SectionName));
            services.AddHostedService<InboxDispatcherBackgroundService>();

            services.Configure<PendingExpiryOptions>(configuration.GetSection(PendingExpiryOptions.SectionName));
            if (configuration.GetValue($"{PendingExpiryOptions.SectionName}:{nameof(PendingExpiryOptions.Enabled)}", true))
                services.AddHostedService<PendingExpiryBackgroundService>();

            services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
            services.AddScoped<BankTransactionsKafkaMessageHandler>();

            // Kafka is an additional inbound channel, not a hard dependency: without a broker
            // configured (tests, a REST-only deployment) the webhook path works unchanged.
            var kafkaEnabled = configuration.GetValue($"{KafkaOptions.SectionName}:{nameof(KafkaOptions.Enabled)}", true);
            if (kafkaEnabled && !string.IsNullOrWhiteSpace(configuration.GetConnectionString(KafkaOptions.ConnectionStringName)))
                services.AddHostedService<BankTransactionsKafkaConsumer>();

            return services;
        }
    }
}