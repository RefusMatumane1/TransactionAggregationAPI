using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Audit.Application.Persistence;
using Modules.Audit.Infrastructure.Persistence;
using Npgsql;

namespace Modules.Audit
{
    public static class AuditInfrastructureDependencyInjection
    {
        public static IServiceCollection AddAuditModule(this IServiceCollection services)
        {
            services.AddAuditApplication();

            services.AddDbContext<AuditDbContext>((sp, options) =>
                options.UseNpgsql(sp.GetRequiredService<NpgsqlConnection>(),
                    npgsql => npgsql.UseModuleDefaults<AuditDbContext>()));
            services.AddScoped<IAuditDbContext>(provider => provider.GetRequiredService<AuditDbContext>());

            return services;
        }
    }
}