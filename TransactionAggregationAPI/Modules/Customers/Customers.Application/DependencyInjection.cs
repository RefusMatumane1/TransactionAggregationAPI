using System.Reflection;
using FluentValidation;
using Mapster;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Modules.Customers.Application.Contracts;
using Modules.Customers.Contracts;

namespace Modules.Customers
{
    public static class CustomersApplicationDependencyInjection
    {
        public static IServiceCollection AddCustomersApplication(this IServiceCollection services)
        {
            services.AddValidatorsFromAssembly(typeof(AssemblyReference).Assembly);

            // Handler registration only — pipeline behaviors (Validation/Logging/
            // Performance/Caching) are registered exactly once, centrally, in
            // AddTransactionsApplication(). Adding them again
            // here would execute every behavior twice for this module's requests.
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(AssemblyReference).Assembly));

            // CustomerProfile (Customer -> CustomerDto) is picked up by Mapster's global
            // config the same way it was when this lived in the legacy
            // Modules.Transactions.Application project — TypeAdapterConfig.GlobalSettings
            // is a shared static, so any module scanning its own assembly into it is safe.
            TypeAdapterConfig.GlobalSettings.Scan(typeof(AssemblyReference).Assembly);

            // CustomersReadApi needs ICustomersDbContext, which is why the read-contract
            // implementation lives here (Application) rather than in Contracts itself —
            // Contracts stays a leaf project with no EF/persistence dependency.
            services.AddScoped<ICustomersReadApi, CustomersReadApi>();

            return services;
        }
    }

    /// <summary>
    /// Anchor type for locating this assembly (MediatR/FluentValidation/Mapster
    /// scanning, architecture tests) without relying on
    /// <see cref="Assembly.GetExecutingAssembly"/>, which would resolve to the wrong
    /// assembly once this method is called from Infrastructure's DependencyInjection.
    /// </summary>
    internal sealed class AssemblyReference;
}
