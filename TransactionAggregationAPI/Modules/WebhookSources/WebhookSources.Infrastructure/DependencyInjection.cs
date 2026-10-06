using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.WebhookSources.Application.Persistence;
using Modules.WebhookSources.Infrastructure.Persistence;
using Npgsql;

namespace Modules.WebhookSources
{
    public static class WebhookSourcesInfrastructureDependencyInjection
    {
        public static IServiceCollection AddWebhookSourcesModule(this IServiceCollection services)
        {
            services.AddWebhookSourcesApplication();

            services.AddDbContext<WebhookSourcesDbContext>((sp, options) =>
                options.UseNpgsql(sp.GetRequiredService<NpgsqlConnection>(),
                    npgsql => npgsql.UseModuleDefaults<WebhookSourcesDbContext>()));
            services.AddScoped<IWebhookSourcesDbContext>(provider => provider.GetRequiredService<WebhookSourcesDbContext>());

            return services;
        }
    }
}