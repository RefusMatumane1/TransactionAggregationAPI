using BuildingBlocks.Application;
using Microsoft.Extensions.DependencyInjection;
using Modules.WebhookSources.Application.Contracts;
using Modules.WebhookSources.Contracts;

namespace Modules.WebhookSources
{
    public static class WebhookSourcesApplicationDependencyInjection
    {
        public static IServiceCollection AddWebhookSourcesApplication(this IServiceCollection services)
        {
            services.AddModuleApplication(typeof(AssemblyReference).Assembly);

            services.AddScoped<IWebhookSourceAuthenticator, WebhookSourceAuthenticator>();
            services.AddScoped<IWebhookSourceDirectory, WebhookSourceDirectory>();

            return services;
        }
    }

    internal sealed class AssemblyReference;
}