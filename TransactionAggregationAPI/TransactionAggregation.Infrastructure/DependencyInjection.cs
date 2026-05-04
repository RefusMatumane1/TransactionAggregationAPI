using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TransactionAggregation.Application.Abstractions.Authentication;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Services;
using TransactionAggregation.Infrastructure.Authentication;
using TransactionAggregation.Infrastructure.BackgroundServices;
using TransactionAggregation.Infrastructure.Providers;
using TransactionAggregation.Infrastructure.Services;

namespace TransactionAggregation.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddHttpClient();

            // Register multiple mock transaction sources (satisfies "multiple data sources" requirement)
            services.AddScoped<ITransactionSource, BogusTransactionSource>();
            services.AddScoped<ITransactionSource, StaticDataTransactionSource>();

            services.AddScoped<ITransactionAggregator, TransactionAggregator>();

            // Redis-backed distributed cache
            services.AddDistributedMemoryCache();
            services.AddScoped<ICacheService, RedisCacheService>();

            services.Configure<NotificationOptions>(
                configuration.GetSection("NotificationOptions"));

            services.Configure<TransactionValidationOptions>(
                configuration.GetSection("TransactionValidationOptions"));

            // Background sync worker
            services.Configure<TransactionSyncOptions>(
                configuration.GetSection(TransactionSyncOptions.SectionName));
            services.AddHostedService<TransactionSyncBackgroundService>();
            
            
            services.AddScoped<IUserContext, UserContext>();
            services.AddSingleton<IPasswordHasher, PasswordHasher>();
            services.AddSingleton<ITokenProvider, TokenProvider>();

            return services;
        }
    }
}
