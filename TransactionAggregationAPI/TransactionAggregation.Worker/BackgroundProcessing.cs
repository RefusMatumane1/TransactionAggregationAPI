using BuildingBlocks.Messaging.Publishing;
using TransactionAggregation.Worker.BackgroundServices;
using TransactionAggregation.Worker.Kafka;
using TransactionAggregation.Worker.Notifications;
using TransactionAggregation.Worker.Outbox;

namespace TransactionAggregation.Worker
{
    // Everything that runs in the background lives in this host; the API only serves HTTP.
    public static class BackgroundProcessing
    {
        public static IServiceCollection AddBackgroundProcessing(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
            services.AddHostedService<OutboxDispatcherBackgroundService>();

            services.Configure<InboxOptions>(configuration.GetSection(InboxOptions.SectionName));
            services.AddHostedService<InboxDispatcherBackgroundService>();

            services.Configure<MessageArchiveOptions>(configuration.GetSection(MessageArchiveOptions.SectionName));
            if (configuration.GetValue($"{MessageArchiveOptions.SectionName}:{nameof(MessageArchiveOptions.Enabled)}", true))
                services.AddHostedService<MessageArchiveBackgroundService>();

            services.Configure<AggregationOptions>(configuration.GetSection(AggregationOptions.SectionName));
            if (configuration.GetValue($"{AggregationOptions.SectionName}:{nameof(AggregationOptions.Enabled)}", true))
                services.AddHostedService<DailyTotalsRefreshBackgroundService>();

            services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
            services.AddScoped<BankTransactionsKafkaMessageHandler>();

            // The inbound bank-transactions consumer can be switched off; publishing cannot.
            if (configuration.GetValue($"{KafkaOptions.SectionName}:{nameof(KafkaOptions.Enabled)}", true))
                services.AddHostedService<BankTransactionsKafkaConsumer>();

            // Outbox message handlers (one per type) and the broker they publish integration events to.
            services.AddScoped<IOutboxMessageHandler, TransactionRecordedHandler>();
            services.AddScoped<IOutboxMessageHandler, DuplicateInboundDetectedHandler>();
            if (string.IsNullOrWhiteSpace(configuration.GetConnectionString(KafkaOptions.ConnectionStringName)))
                throw new InvalidOperationException(
                    $"ConnectionStrings:{KafkaOptions.ConnectionStringName} must be configured: the outbox publishes integration events to Kafka.");
            services.AddSingleton<IIntegrationEventPublisher, KafkaIntegrationEventPublisher>();

            // Duplicate-delivery alerts. No request loggers: the URL embeds the hook's credential.
            services.AddHttpClient(NotificationService.HttpClientName).RemoveAllLoggers();
            services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
            services.AddScoped<INotificationService, NotificationService>();

            return services;
        }
    }
}