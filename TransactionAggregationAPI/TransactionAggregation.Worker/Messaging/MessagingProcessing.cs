using BuildingBlocks.Messaging.Publishing;

namespace TransactionAggregation.Worker.Messaging
{
    internal static class MessagingProcessing
    {
        /// <summary>The shared reliability plumbing: outbox dispatch, archiving, and the Kafka publisher the outbox uses.</summary>
        public static IServiceCollection AddMessagingProcessing(this IServiceCollection services, IConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration.GetConnectionString(KafkaOptions.ConnectionStringName)))
                throw new InvalidOperationException(
                    $"ConnectionStrings:{KafkaOptions.ConnectionStringName} must be configured: the outbox publishes integration events to Kafka.");

            services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
            services.AddSingleton<IIntegrationEventPublisher, KafkaIntegrationEventPublisher>();

            services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
            services.AddHostedService<OutboxDispatcherBackgroundService>();

            services.Configure<MessageArchiveOptions>(configuration.GetSection(MessageArchiveOptions.SectionName));
            services.AddHostedServiceIfEnabled<MessageArchiveBackgroundService>(configuration, MessageArchiveOptions.SectionName);

            return services;
        }
    }
}
