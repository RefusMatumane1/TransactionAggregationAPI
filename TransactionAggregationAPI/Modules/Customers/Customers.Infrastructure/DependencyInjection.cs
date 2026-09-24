using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Modules.BankLinks.Contracts;
using Modules.Customers.Application.Adapters;
using Modules.Customers.Application.Persistence;
using Modules.Customers.Application.Ports;
using Modules.Customers.Infrastructure.Authentication;
using Modules.Customers.Infrastructure.Persistence;

namespace Modules.Customers
{
    public static class CustomersInfrastructureDependencyInjection
    {
        public static IServiceCollection AddCustomersModule(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddCustomersApplication();

            var connectionString = configuration.GetConnectionString("transactiondb");

            services.AddDbContext<CustomersDbContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly("Customers.Infrastructure");
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                });
            });

            services.AddScoped<ICustomersDbContext>(provider =>
                provider.GetRequiredService<CustomersDbContext>());

            // Implements the port BankLinks owns — see docs/adr/0010-consumer-owned-ports-for-unextracted-dependencies.md.
            services.AddScoped<IAccountProvisioningPort, AccountProvisioningAdapter>();

            services.Configure<KeycloakOptions>(configuration.GetSection(KeycloakOptions.SectionName));
            services.AddHttpClient<IKeycloakAdminClient, KeycloakAdminClient>((sp, client) =>
            {
                var keycloakOptions = sp.GetRequiredService<IOptions<KeycloakOptions>>().Value;
                client.BaseAddress = new Uri(keycloakOptions.Authority.TrimEnd('/') + "/");
            });

            return services;
        }
    }
}