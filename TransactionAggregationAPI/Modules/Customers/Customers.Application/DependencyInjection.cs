using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Customers.Application.Contracts;
using Modules.Customers.Contracts;

namespace Modules.Customers
{
    public static class CustomersApplicationDependencyInjection
    {
        public static IServiceCollection AddCustomersApplication(this IServiceCollection services)
        {
            services.AddModuleApplication(typeof(AssemblyReference).Assembly);

            services.AddScoped<ICustomerAccounts, CustomerAccounts>();
            services.AddScoped<ICustomerViewAudit, CustomerViewAudit>();

            return services;
        }
    }

    internal sealed class AssemblyReference;
}