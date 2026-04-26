using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Services;
using TransactionAggregation.Infrastructure.Services;

namespace TransactionAggregation.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Add HTTP client factory
            services.AddHttpClient();

            // Register transaction sources
            //services.AddScoped<ITransactionSource, BankATransactionSource>();
            //services.AddScoped<ITransactionSource, BankBTransactionSource>();
            //services.AddScoped<ITransactionSource, WalletTransactionSource>();

            services.AddScoped<ITransactionAggregator, TransactionAggregator>();

            // Add Redis caching
            services.AddDistributedMemoryCache();

            services.AddScoped<ICacheService>(sp =>
            {
                var redis = sp.GetRequiredService<IConnectionMultiplexer>();
                var logger = sp.GetRequiredService<ILogger<RedisCacheService>>();
                return new RedisCacheService(redis, logger);
            });

            services.AddScoped<ICacheService, RedisCacheService>();

            // Application services (implementations in Application layer)
            services.AddScoped<IAnalyticsService, AnalyticsService>();
            services.AddScoped<INotificationService, NotificationService>();

            // Infrastructure services
          //  services.AddScoped<IMetricsCollector, MetricsCollector>();
            services.AddScoped<ITransactionValidator, TransactionValidator>();
            //services.AddScoped<IRuleBasedCategorizationStrategy, RuleBasedCategorizationStrategy>();
            //services.AddScoped<ITransactionCategorizationStrategy, MLCategorizationService>();

            // Configure options
            services.Configure<NotificationOptions>(
                configuration.GetSection("NotificationOptions"));

            services.Configure<TransactionValidationOptions>(
                configuration.GetSection("TransactionValidationOptions"));

            return services;
        }
    }
}
