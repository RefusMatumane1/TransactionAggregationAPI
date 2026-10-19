using BuildingBlocks.Messaging.Archiving;
using BuildingBlocks.Messaging.Persistence;
using BuildingBlocks.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.Messaging
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddMessagingBuildingBlock(this IServiceCollection services)
        {
            services.AddModuleDbContext<MessagingDbContext, IMessagingDbContext>();
            services.AddScoped<IMessageArchive>(provider =>
                provider.GetRequiredService<MessagingDbContext>());

            return services;
        }
    }
}