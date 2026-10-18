using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Notifications.Application.Common;
using Modules.Notifications.Application.Persistence;
using Modules.Notifications.Infrastructure;
using Modules.Notifications.Infrastructure.Persistence;
using Npgsql;

namespace Modules.Notifications
{
    public static class NotificationsInfrastructureDependencyInjection
    {
        public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddNotificationsApplication(configuration);

            services.AddDbContext<NotificationsDbContext>((sp, options) =>
                options.UseNpgsql(sp.GetRequiredService<NpgsqlConnection>(),
                    npgsql => npgsql.UseModuleDefaults<NotificationsDbContext>()));
            services.AddScoped<INotificationsDbContext>(provider => provider.GetRequiredService<NotificationsDbContext>());

            services.AddHttpClient(AlertWebhookPoster.HttpClientName).RemoveAllLoggers();
            services.AddScoped<IAlertWebhookPoster, AlertWebhookPoster>();

            return services;
        }
    }
}