using BuildingBlocks.Application.Seeding;
using BuildingBlocks.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Modules.WebhookSources.Application.Persistence;
using Modules.WebhookSources.Infrastructure.Development;
using Modules.WebhookSources.Infrastructure.Persistence;

namespace Modules.WebhookSources
{
    public static class WebhookSourcesInfrastructureDependencyInjection
    {
        public static IServiceCollection AddWebhookSourcesModule(this IServiceCollection services, bool isDevelopment = false)
        {
            services.AddWebhookSourcesApplication();

            services.AddModuleDbContext<WebhookSourcesDbContext, IWebhookSourcesDbContext>();

            if (isDevelopment)
                services.AddDevelopmentSeeder<MockAggregatorBankSeeder>();

            return services;
        }
    }
}