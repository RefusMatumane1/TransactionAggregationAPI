using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Audit.Application.Persistence;
using Modules.Audit.Infrastructure.Persistence;

namespace Modules.Audit
{
    public static class AuditInfrastructureDependencyInjection
    {
        public static IServiceCollection AddAuditModule(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddAuditApplication();

            var connectionString = configuration.GetConnectionString("transactiondb");

            services.AddDbContext<AuditDbContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly("Audit.Infrastructure");
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                });
            });

            services.AddScoped<IAuditDbContext>(provider =>
                provider.GetRequiredService<AuditDbContext>());

            return services;
        }
    }
}