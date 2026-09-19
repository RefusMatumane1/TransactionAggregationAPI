using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel.Abstractions.Authentication;
using TransactionAggregation.Application.Abstractions.Authentication;
using SharedKernel.Common.Interfaces;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Options;
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

            services.Configure<BankAggregatorOptions>(
                            configuration.GetSection(BankAggregatorOptions.SectionName));
            services.AddScoped<IBankLinkCredentialProtector, BankLinkCredentialProtector>();

            services.AddHttpClient<IBankAggregatorClient, HttpBankAggregatorClient>();

            services.AddDistributedMemoryCache();
            services.AddScoped<ICacheService, RedisCacheService>();

            services.Configure<NotificationOptions>(
                configuration.GetSection("NotificationOptions"));
            services.AddScoped<INotificationService, NotificationService>();

            services.AddScoped<IUserContext, UserContext>();

            services.Configure<KeycloakOptions>(configuration.GetSection(KeycloakOptions.SectionName));
            services.AddHttpClient<IKeycloakAdminClient, KeycloakAdminClient>((sp, client) =>
            {
                var keycloakOptions = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
                client.BaseAddress = new Uri(keycloakOptions.Authority.TrimEnd('/') + "/");
            });

            services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
            services.AddHostedService<OutboxDispatcherBackgroundService>();

            services.Configure<InboxOptions>(configuration.GetSection(InboxOptions.SectionName));
            services.AddHostedService<InboxDispatcherBackgroundService>();

            return services;
        }
    }
}