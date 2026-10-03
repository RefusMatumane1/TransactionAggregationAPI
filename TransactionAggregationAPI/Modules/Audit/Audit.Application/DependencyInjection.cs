using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.Audit.Application.Contracts;
using Modules.Audit.Contracts;

namespace Modules.Audit
{
    public static class AuditApplicationDependencyInjection
    {
        public static IServiceCollection AddAuditApplication(this IServiceCollection services)
        {
            services.AddModuleApplication(typeof(AssemblyReference).Assembly);

            services.AddScoped<IAuditTrail, AuditTrail>();

            return services;
        }
    }

    internal sealed class AssemblyReference;
}