using FluentValidation;
using MediatR;
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

            // Handler registration only — pipeline behaviors (Validation/Logging/
            // Performance/Caching) are registered exactly once, centrally, in
            // AddTransactionsApplication(). Adding them again
            // here would execute every behavior twice for this module's requests.
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(AssemblyReference).Assembly));

            services.AddScoped<IWebhookSourceAuthenticator, WebhookSourceAuthenticator>();
            services.AddScoped<IWebhookSourceDirectory, WebhookSourceDirectory>();

            return services;
        }
    }

    /// <summary>
    /// Anchor type for locating this assembly (MediatR/FluentValidation scanning,
    /// architecture tests) without relying on <see cref="Assembly.GetExecutingAssembly"/>,
    /// which would resolve to the wrong assembly once this method is called from
    /// Infrastructure's DependencyInjection.
    /// </summary>
    internal sealed class AssemblyReference;
}