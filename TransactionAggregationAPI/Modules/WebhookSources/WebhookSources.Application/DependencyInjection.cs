using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Modules.WebhookSources.Application.Contracts;
using Modules.WebhookSources.Contracts;
using System.Reflection;

namespace Modules.WebhookSources
{
    public static class WebhookSourcesApplicationDependencyInjection
    {
        public static IServiceCollection AddWebhookSourcesApplication(this IServiceCollection services)
        {
            services.AddValidatorsFromAssembly(typeof(AssemblyReference).Assembly);

            // Handlers only: pipeline behaviors are registered once in AddTransactionsApplication();
            // registering them here too would run each behavior twice.
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(AssemblyReference).Assembly));

            services.AddScoped<IWebhookSourceAuthenticator, WebhookSourceAuthenticator>();
            services.AddScoped<IWebhookSourceDirectory, WebhookSourceDirectory>();

            return services;
        }
    }

    /// <summary>Anchor type for locating this assembly (MediatR/FluentValidation scanning, architecture tests).</summary>
    internal sealed class AssemblyReference;
}