using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Modules.Audit.Application.Contracts;
using Modules.Audit.Contracts;
using System.Reflection;

namespace Modules.Audit
{
    public static class AuditApplicationDependencyInjection
    {
        public static IServiceCollection AddAuditApplication(this IServiceCollection services)
        {
            services.AddValidatorsFromAssembly(typeof(AssemblyReference).Assembly);

            // Handlers only: pipeline behaviors are registered once in AddTransactionsApplication();
            // registering them here too would run each behavior twice.
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(AssemblyReference).Assembly));

            services.AddScoped<IAuditTrail, AuditTrail>();

            return services;
        }
    }

    /// <summary>Anchor type for locating this assembly (MediatR/FluentValidation scanning, architecture tests).</summary>
    internal sealed class AssemblyReference;
}