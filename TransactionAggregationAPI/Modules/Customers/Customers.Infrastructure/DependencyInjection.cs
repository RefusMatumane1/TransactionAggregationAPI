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

            services.AddDbContext<CustomersDbContext>((sp, options) =>
                options.UseNpgsql(sp.GetRequiredService<NpgsqlConnection>(),
                    npgsql => npgsql.UseModuleDefaults<CustomersDbContext>()));
            services.AddScoped<ICustomersDbContext>(provider => provider.GetRequiredService<CustomersDbContext>());

            return services;
        }
    }
}