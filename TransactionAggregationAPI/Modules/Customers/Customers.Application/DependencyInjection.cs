using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Modules.Customers.Application.Contracts;
using Modules.Customers.Contracts;
using System.Reflection;

namespace Modules.Customers
{
    public static class CustomersApplicationDependencyInjection
    {
        public static IServiceCollection AddCustomersApplication(this IServiceCollection services)
        {
            services.AddValidatorsFromAssembly(typeof(AssemblyReference).Assembly);

            // Handlers only: pipeline behaviors are registered once in AddTransactionsApplication();
            // registering them here too would run each behavior twice.
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(AssemblyReference).Assembly));

            services.AddScoped<ICustomersReadApi, CustomersReadApi>();

            return services;
        }
    }

    /// <summary>Anchor type for locating this assembly (MediatR/FluentValidation scanning, architecture tests).</summary>
    internal sealed class AssemblyReference;
}