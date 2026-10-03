using BuildingBlocks.Messaging.Archiving;
using BuildingBlocks.Messaging.Persistence;
using BuildingBlocks.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BuildingBlocks.Messaging
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddMessagingBuildingBlock(this IServiceCollection services)
        {
            services.AddDbContext<MessagingDbContext>((sp, options) =>
            {
                var connection = sp.GetRequiredService<NpgsqlConnection>();
                options.UseNpgsql(connection, npgsql => npgsql.UseModuleDefaults<MessagingDbContext>());
            });

            services.AddScoped<IMessagingDbContext>(provider =>
                provider.GetRequiredService<MessagingDbContext>());
            services.AddScoped<IMessageArchive>(provider =>
                provider.GetRequiredService<MessagingDbContext>());

            return services;
        }
    }
}