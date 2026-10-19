using TransactionAggregation.Worker.Messaging;

namespace TransactionAggregation.Worker.Notifications
{
    internal static class NotificationsProcessing
    {
        /// <summary>Turns outbox events into notifications.</summary>
        public static IServiceCollection AddNotificationsProcessing(this IServiceCollection services)
        {
            services.AddScoped<IOutboxMessageHandler, DuplicateInboundDetectedHandler>();

            return services;
        }
    }
}
