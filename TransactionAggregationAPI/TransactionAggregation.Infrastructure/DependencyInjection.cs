using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TransactionAggregation.Application.Abstractions.Authentication;
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

            // Real bank connectivity via a licensed account-data aggregator — it pushes
            // transaction data to us (see WebhookEndpoints), so this client only backs the
            // consent/linking handshake (InitiateBankLink/CompleteBankLink), not a pull loop.
            services.Configure<BankAggregatorOptions>(
                configuration.GetSection(BankAggregatorOptions.SectionName));
            services.AddScoped<IBankLinkCredentialProtector, BankLinkCredentialProtector>();
            // Resilience (retry/circuit-breaker/timeout) is inherited from the standard
            // handler ServiceDefaults registers for every HttpClient — see AddServiceDefaults.
            services.AddHttpClient<IBankAggregatorClient, HttpBankAggregatorClient>();

            // Redis-backed distributed cache
            services.AddDistributedMemoryCache();
            services.AddScoped<ICacheService, RedisCacheService>();

            services.Configure<NotificationOptions>(
                configuration.GetSection("NotificationOptions"));
            services.AddScoped<INotificationService, NotificationService>();

            services.AddScoped<IUserContext, UserContext>();

            // Keycloak is the sole identity/credential store — see IKeycloakAdminClient.
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
