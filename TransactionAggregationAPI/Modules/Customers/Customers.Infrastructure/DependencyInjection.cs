using BuildingBlocks.Application.Seeding;
using BuildingBlocks.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Modules.Customers.Application.Persistence;
using Modules.Customers.Infrastructure.Development;
using Modules.Customers.Infrastructure.Persistence;

namespace Modules.Customers
{
    public static class CustomersInfrastructureDependencyInjection
    {
        public static IServiceCollection AddCustomersModule(this IServiceCollection services, bool isDevelopment = false)
        {
            services.AddCustomersApplication();

            services.AddModuleDbContext<CustomersDbContext, ICustomersDbContext>();

            if (isDevelopment)
                services.AddDevelopmentSeeder<CustomersDemoSeeder>();

            return services;
        }
    }
}