using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.WebhookSources.Application.Persistence;
using Modules.WebhookSources.Infrastructure.Persistence;

namespace Modules.WebhookSources
{
    public static class WebhookSourcesInfrastructureDependencyInjection
    {
        public static IServiceCollection AddWebhookSourcesModule(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            services.AddWebhookSourcesApplication();

            var connectionString = configuration.GetConnectionString("transactiondb");

            services.AddDbContext<WebhookSourcesDbContext>(options =>
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly("WebhookSources.Infrastructure");
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorCodesToAdd: null);
                });
            });

            services.AddScoped<IWebhookSourcesDbContext>(provider =>
                provider.GetRequiredService<WebhookSourcesDbContext>());

            return services;
        }
    }
}
