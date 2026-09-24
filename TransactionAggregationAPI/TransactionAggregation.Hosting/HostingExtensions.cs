using BuildingBlocks.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Audit;
using Modules.BankLinks;
using Modules.Customers;
using Modules.Transactions;
using Modules.Transactions.Infrastructure.Persistence;
using Modules.WebhookSources;
using Npgsql;
using Serilog;
using SharedKernel.Common.Interfaces;
using StackExchange.Redis;
using TransactionAggregation.Hosting.Caching;
using TransactionAggregation.Hosting.Logging;

namespace TransactionAggregation.Hosting
{
    public static class HostingExtensions
    {
        /// <summary>
        /// Data Protection keys are shared by every process that reads bank-link credentials,
        /// so the application name must be identical in all of them — not the per-host
        /// ApplicationName, which differs between the API and the worker.
        /// </summary>
        private const string DataProtectionApplicationName = "TransactionAggregationAPI";

        private const string DataProtectionKeysKey = "DataProtection-Keys";
        private const string DistributedCacheInstanceName = "tagg:";

        private const string CategorizationRulesFile = "categorization-rules.json";
        private const string NormalizationRulesFile = "normalization-rules.json";

        public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder)
        {
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .Destructure.With<SensitiveDataDestructuringPolicy>()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
                .CreateLogger();

            builder.Logging.AddSerilog(Log.Logger, dispose: true);

            builder.Services.AddSingleton<Serilog.ILogger>(Log.Logger);
            builder.Services.AddSingleton<Serilog.Extensions.Hosting.DiagnosticContext>();
            builder.Services.AddSingleton<IDiagnosticContext>(
                sp => sp.GetRequiredService<Serilog.Extensions.Hosting.DiagnosticContext>());

            return builder;
        }

        /// <summary>
        /// Everything the business modules need from their host: the shared Postgres
        /// connection, every module's registrations, Redis (cache + Data Protection key ring)
        /// and the ingestion rules (normalization and categorization). Host-specific concerns — HTTP pipeline, auth,
        /// background processing — stay in each host's Program.cs.
        /// </summary>
        public static WebApplicationBuilder AddApplicationModules(this WebApplicationBuilder builder)
        {
            builder.AddRulesFile(CategorizationRulesFile);
            builder.AddRulesFile(NormalizationRulesFile);

            var connectionString = builder.Configuration.GetConnectionString("transactiondb");

            // TransactionsDbContext and MessagingDbContext share one scoped NpgsqlConnection
            // (rather than each opening its own) so TransactionsDbContext.SaveChangesAsync can
            // share one real transaction across both — required for an Outbox write to commit
            // atomically with the business-entity change that triggered it. See
            // docs/adr/0009-schema-per-module-database-strategy.md. WebhookSourcesDbContext
            // has no such requirement (every write there is a standalone unit of work) and
            // keeps its own independent connection, registered inside AddWebhookSourcesModule.
            builder.Services.AddScoped(_ => new NpgsqlConnection(connectionString));

            builder.Services.AddMessagingBuildingBlock(builder.Configuration);
            builder.Services.AddAuditModule(builder.Configuration);
            builder.Services.AddWebhookSourcesModule(builder.Configuration);
            builder.Services.AddBankLinksModule(builder.Configuration);
            builder.Services.AddCustomersModule(builder.Configuration);
            builder.Services.AddTransactionsModule(builder.Configuration, builder.Environment.IsDevelopment());

            builder.EnrichNpgsqlDbContext<TransactionsDbContext>();

            builder.AddRedisClient("redis", configureSettings: s => s.DisableHealthChecks = true);
            builder.AddSharedState();

            // Host-level services shared by every module (not owned by any one of them).
            builder.Services.AddScoped<ICacheService, RedisCacheService>();

            return builder;
        }

        /// <summary>
        /// Loaded as the lowest-precedence source, so appsettings.*.json and environment
        /// variables can still override individual rules per host or environment. An optional
        /// "{name}.{Environment}.json" sits directly above its base file — e.g. the mock banks'
        /// rules that only Development loads.
        /// </summary>
        private static void AddRulesFile(this WebApplicationBuilder builder, string fileName)
        {
            var environmentFile = Path.ChangeExtension(fileName, $"{builder.Environment.EnvironmentName}.json");

            builder.Configuration.Sources.Insert(0, RulesFileSource(fileName, optional: false));
            builder.Configuration.Sources.Insert(1, RulesFileSource(environmentFile, optional: true));
        }

        private static JsonConfigurationSource RulesFileSource(string fileName, bool optional)
        {
            var source = new JsonConfigurationSource
            {
                Path = Path.Combine(AppContext.BaseDirectory, fileName),
                Optional = optional,
                ReloadOnChange = false
            };
            source.ResolveFileProvider();
            return source;
        }

        /// <summary>
        /// Redis backs the two pieces of state that must be shared by every replica and survive
        /// restarts: the Data Protection key ring (bank-link tokens are encrypted with it) and
        /// IDistributedCache (OAuth state between bank-link initiation and its callback, which
        /// can land on a different pod). Both reuse the Aspire-managed IConnectionMultiplexer
        /// rather than opening their own connections.
        ///
        /// Outside Development a missing Redis connection is a startup failure, not a warning:
        /// the previous ephemeral-key fallback let a misconfigured production pod start
        /// "healthy" and then make every stored bank-link token undecryptable on its next
        /// restart, and an in-process cache broke bank linking whenever the API ran more than
        /// one replica. Development keeps both fallbacks so the app runs without Redis.
        /// </summary>
        private static void AddSharedState(this WebApplicationBuilder builder)
        {
            var dataProtection = builder.Services.AddDataProtection()
                .SetApplicationName(DataProtectionApplicationName);

            if (string.IsNullOrEmpty(builder.Configuration.GetConnectionString("redis")))
            {
                if (!builder.Environment.IsDevelopment())
                    throw new InvalidOperationException(
                        "ConnectionStrings:redis must be configured outside Development: it holds the Data Protection key ring " +
                        "that bank-link tokens are encrypted with and the OAuth state shared between API replicas.");

                Log.Warning("No Redis connection string configured (Development) — using ephemeral Data Protection keys and an in-process cache.");
                dataProtection.UseEphemeralDataProtectionProvider();
                builder.Services.AddDistributedMemoryCache();
                return;
            }

            builder.Services.AddOptions<KeyManagementOptions>()
                .Configure<IConnectionMultiplexer>((options, redis) =>
                    options.XmlRepository = new RedisXmlRepository(() => redis.GetDatabase(), DataProtectionKeysKey));

            builder.Services.AddStackExchangeRedisCache(options => options.InstanceName = DistributedCacheInstanceName);
            builder.Services.AddOptions<RedisCacheOptions>()
                .Configure<IConnectionMultiplexer>((options, redis) =>
                    options.ConnectionMultiplexerFactory = () => Task.FromResult(redis));
        }
    }
}