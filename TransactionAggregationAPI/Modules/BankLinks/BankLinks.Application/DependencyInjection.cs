using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Modules.BankLinks.Application.Contracts;
using Modules.BankLinks.Contracts;

namespace Modules.BankLinks
{
    public static class BankLinksApplicationDependencyInjection
    {
        public static IServiceCollection AddBankLinksApplication(this IServiceCollection services)
        {
            services.AddValidatorsFromAssembly(typeof(AssemblyReference).Assembly);

            // Handler registration only — pipeline behaviors (Validation/Logging/
            // Performance/Caching) are registered exactly once, centrally, in
            // TransactionAggregation.Application.AddApplication(). Adding them again
            // here would execute every behavior twice for this module's requests.
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(AssemblyReference).Assembly));

            // BankLinksReadApi needs IBankLinksDbContext, which is why the read-contract
            // implementation lives here (Application) rather than in Contracts itself —
            // Contracts stays a leaf project with no EF/persistence dependency.
            services.AddScoped<IBankLinksReadApi, BankLinksReadApi>();

            return services;
        }
    }

    /// <summary>
    /// Anchor type for locating this assembly (MediatR/FluentValidation scanning,
    /// architecture tests) without relying on <see cref="Assembly.GetExecutingAssembly"/>,
    /// which would resolve to the wrong assembly once this method is called from
    /// Infrastructure's DependencyInjection.
    /// </summary>
    internal sealed class AssemblyReference;
}
