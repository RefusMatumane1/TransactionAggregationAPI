using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Customers.Application.Persistence;
using Modules.Customers.Infrastructure.Persistence;
using Npgsql;

namespace Modules.Customers
{
    public static class CustomersInfrastructureDependencyInjection
    {
        public static IServiceCollection AddCustomersModule(this IServiceCollection services)
        {
            services.AddCustomersApplication();

            // On the scoped shared connection so a change and its audit row commit in one transaction.
            services.AddDbContext<CustomersDbContext>((sp, options) =>
                options.UseNpgsql(sp.GetRequiredService<NpgsqlConnection>(),
                    npgsql => npgsql.UseModuleDefaults<CustomersDbContext>()));
            services.AddScoped<ICustomersDbContext>(provider => provider.GetRequiredService<CustomersDbContext>());

            return services;
        }
    }
}