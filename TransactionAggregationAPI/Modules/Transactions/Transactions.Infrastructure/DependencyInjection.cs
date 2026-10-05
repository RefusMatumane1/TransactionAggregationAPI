using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Infrastructure.Persistence;
using Npgsql;

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

            services.AddDbContext<TransactionsDbContext>((sp, options) =>
            {
                options.UseNpgsql(sp.GetRequiredService<NpgsqlConnection>(),
                    npgsql => npgsql.UseModuleDefaults<TransactionsDbContext>());

                if (isDevelopment)
                {
                    options.EnableDetailedErrors();
                    options.EnableSensitiveDataLogging();
                }
            });

            services.AddScoped<ITransactionsDbContext>(provider =>
                provider.GetRequiredService<TransactionsDbContext>());
            services.AddSingleton<ITransactionSearch, PostgresTransactionSearch>();
            services.AddScoped<IDailyTotalsRefresher, PostgresDailyTotalsRefresher>();

            return services;
        }
    }
}