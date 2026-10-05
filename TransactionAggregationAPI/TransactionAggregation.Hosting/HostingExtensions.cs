using BuildingBlocks.Application;
using BuildingBlocks.Application.Caching;
using BuildingBlocks.Application.Pagination;
using BuildingBlocks.Messaging;
using BuildingBlocks.Persistence;
using BuildingBlocks.Persistence.Pagination;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Modules.Audit;
using Modules.Transactions;
using Modules.Transactions.Infrastructure.Persistence;
using Modules.WebhookSources;
using Npgsql;
using Serilog;
using StackExchange.Redis;
using TransactionAggregation.Hosting.Caching;
using TransactionAggregation.Hosting.Health;
using TransactionAggregation.Hosting.Logging;

namespace TransactionAggregation.Hosting
{
    public static class HostingExtensions
    {
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

        public static WebApplicationBuilder AddApplicationModules(this WebApplicationBuilder builder)
        {
            builder.AddRulesFile(CategorizationRulesFile);
            builder.AddRulesFile(NormalizationRulesFile);

            var connectionString = builder.Configuration.GetConnectionString(ModuleDbContextRegistration.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    $"ConnectionStrings:{ModuleDbContextRegistration.ConnectionStringName} must be configured: PostgreSQL is the system of record.");

            builder.Services.AddScoped(_ => new NpgsqlConnection(connectionString));

            builder.Services.AddApplicationPipeline(
                builder.Configuration.GetSection(CachingOptions.SectionName).Get<CachingOptions>());
            builder.Services.AddSingleton<IKeysetPaginator, RowValueKeysetPaginator>();
            builder.Services.TryAddSingleton(TimeProvider.System);
            builder.Services.AddMessagingBuildingBlock();
            builder.Services.AddAuditModule();
            builder.Services.AddWebhookSourcesModule();
            builder.Services.AddTransactionsModule(builder.Configuration, builder.Environment.IsDevelopment());

            builder.EnrichNpgsqlDbContext<TransactionsDbContext>(settings => settings.DisableHealthChecks = true);
            builder.Services.AddHealthChecks()
                .AddCheck<PostgresHealthCheck>("postgres", HealthStatus.Unhealthy, [Microsoft.Extensions.Hosting.Extensions.ReadyTag]);

            builder.AddRedisClient("redis", configureSettings: s => s.DisableHealthChecks = true);
            builder.AddSharedState();

            builder.Services.AddScoped<ICacheService, RedisCacheService>();

            return builder;
        }

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

        private static void AddSharedState(this WebApplicationBuilder builder)
        {
            var dataProtection = builder.Services.AddDataProtection()
                .SetApplicationName(DataProtectionApplicationName);

            if (string.IsNullOrEmpty(builder.Configuration.GetConnectionString("redis")))
            {
                if (!builder.Environment.IsDevelopment())
                    throw new InvalidOperationException(
                        "ConnectionStrings:redis must be configured outside Development: it holds the Data Protection key ring " +
                        "and the response cache shared between API replicas.");

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