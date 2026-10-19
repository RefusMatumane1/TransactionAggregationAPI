using BuildingBlocks.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Notifications.Application.Common;
using Modules.Notifications.Application.Persistence;
using Modules.Notifications.Infrastructure;
using Modules.Notifications.Infrastructure.Persistence;
using Modules.Notifications.Infrastructure.Security;

namespace Modules.Notifications
{
    public static class NotificationsInfrastructureDependencyInjection
    {
        public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddNotificationsApplication(configuration);

            services.AddModuleDbContext<NotificationsDbContext, INotificationsDbContext>();

            services.AddSingleton<IAlertUrlProtector, DataProtectionAlertUrlProtector>();

            services.AddHttpClient(AlertWebhookPoster.HttpClientName).RemoveAllLoggers();
            services.AddScoped<IAlertWebhookPoster, AlertWebhookPoster>();

            return services;
        }
    }
}