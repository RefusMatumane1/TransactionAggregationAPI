using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.BankLinks.Application.Persistence;
using Modules.BankLinks.Application.Ports;
using Modules.BankLinks.Infrastructure.Authentication;
using Modules.BankLinks.Infrastructure.Persistence;
using Modules.BankLinks.Infrastructure.Providers;

namespace Modules.BankLinks
{
    public static class BankLinksInfrastructureDependencyInjection
    {
        public static IServiceCollection AddBankLinksModule(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddBankLinksApplication();

            var connectionString = configuration.GetConnectionString("transactiondb");

            services.AddDbContext<BankLinksDbContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly("Modules.BankLinks.Infrastructure");
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                });
            });

            services.AddScoped<IBankLinksDbContext>(provider =>
                provider.GetRequiredService<BankLinksDbContext>());

            services.Configure<BankAggregatorOptions>(
                configuration.GetSection(BankAggregatorOptions.SectionName));
            services.AddScoped<IBankLinkCredentialProtector, BankLinkCredentialProtector>();
            services.AddHttpClient<IBankAggregatorClient, HttpBankAggregatorClient>();

            return services;
        }
    }
}
