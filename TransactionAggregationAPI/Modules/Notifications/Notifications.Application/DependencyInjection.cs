using BuildingBlocks.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Notifications.Application.Common;
using Modules.Notifications.Application.Contracts;
using Modules.Notifications.Contracts;

namespace Modules.Notifications
{
    public static class NotificationsApplicationDependencyInjection
    {
        public static IServiceCollection AddNotificationsApplication(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddModuleApplication(typeof(AssemblyReference).Assembly);

            services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
            services.AddScoped<IDuplicateAlertNotifier, DuplicateAlertNotifier>();

            return services;
        }
    }

    internal sealed class AssemblyReference;
}