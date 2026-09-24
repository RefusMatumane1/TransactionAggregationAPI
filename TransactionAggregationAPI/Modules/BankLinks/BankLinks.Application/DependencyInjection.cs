using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Modules.BankLinks.Application.Contracts;
using Modules.BankLinks.Contracts;
using System.Reflection;

namespace Modules.BankLinks
{
    public static class BankLinksApplicationDependencyInjection
    {
        public static IServiceCollection AddBankLinksApplication(this IServiceCollection services)
        {
            services.AddValidatorsFromAssembly(typeof(AssemblyReference).Assembly);

            // Handlers only: pipeline behaviors are registered once in AddTransactionsApplication();
            // registering them here too would run each behavior twice.
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(AssemblyReference).Assembly));

            services.AddScoped<IBankLinksReadApi, BankLinksReadApi>();

            return services;
        }
    }

    /// <summary>Anchor type for locating this assembly (MediatR/FluentValidation scanning, architecture tests).</summary>
    internal sealed class AssemblyReference;
}