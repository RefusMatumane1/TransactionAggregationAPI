using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.BankLinks.Contracts;
using Modules.BankLinks.Persistence;

namespace Modules.BankLinks
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddBankLinksModule(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("transactiondb");

            services.AddDbContext<BankLinksDbContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly("Modules.BankLinks");
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                });
            });

            services.AddScoped<IBankLinksDbContext>(provider =>
                provider.GetRequiredService<BankLinksDbContext>());

            services.AddScoped<IBankLinksReadApi, BankLinksReadApi>();

            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

            // Handler registration only — pipeline behaviors (Validation/Logging/
            // Performance/Caching) are registered exactly once, centrally, in
            // TransactionAggregation.Application.AddApplication(). Adding them again
            // here would execute every behavior twice for this module's requests.
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

            return services;
        }
    }
}
