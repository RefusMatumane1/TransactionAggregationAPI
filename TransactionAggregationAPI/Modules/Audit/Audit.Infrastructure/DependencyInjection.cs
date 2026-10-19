using BuildingBlocks.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Modules.Audit.Application.Persistence;
using Modules.Audit.Infrastructure.Persistence;

namespace Modules.Audit
{
    public static class AuditInfrastructureDependencyInjection
    {
        public static IServiceCollection AddAuditModule(this IServiceCollection services)
        {
            services.AddAuditApplication();

            services.AddModuleDbContext<AuditDbContext, IAuditDbContext>();

            return services;
        }
    }
}