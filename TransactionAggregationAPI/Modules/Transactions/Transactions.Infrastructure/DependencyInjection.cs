using BuildingBlocks.Application.Seeding;
using BuildingBlocks.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Contracts;
using Modules.Transactions.Infrastructure.Development;
using Modules.Transactions.Infrastructure.Persistence;

namespace Modules.Transactions
{
    public static class TransactionsInfrastructureDependencyInjection
    {
        public static IServiceCollection AddTransactionsModule(
            this IServiceCollection services,
            IConfiguration configuration,
            bool isDevelopment = false)
        {
            services.AddTransactionsApplication(configuration);

            services.AddModuleDbContext<TransactionsDbContext, ITransactionsDbContext>(options =>
            {
                if (isDevelopment)
                {
                    options.EnableDetailedErrors();
                    options.EnableSensitiveDataLogging();
                }
            });
            services.AddSingleton<ITransactionSearch, PostgresTransactionSearch>();

            if (isDevelopment)
            {
                services.AddSingleton<IDemoAccountDirectory, DemoAccountDirectory>();
                services.AddDevelopmentSeeder<TransactionsDemoSeeder>();
            }

            return services;
        }
    }
}