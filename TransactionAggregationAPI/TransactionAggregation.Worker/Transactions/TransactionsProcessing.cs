using TransactionAggregation.Worker.Messaging;

namespace TransactionAggregation.Worker.Transactions
{
    internal static class TransactionsProcessing
    {
        /// <summary>Inbound transaction processing: the inbox dispatcher, the signed Kafka feed into it, and the event the outbox publishes once a transaction is recorded.</summary>
        public static IServiceCollection AddTransactionsProcessing(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<InboxOptions>(configuration.GetSection(InboxOptions.SectionName));
            services.AddHostedService<InboxDispatcherBackgroundService>();

            services.AddScoped<BankTransactionsKafkaMessageHandler>();
            services.AddHostedServiceIfEnabled<BankTransactionsKafkaConsumer>(configuration, KafkaOptions.SectionName);

            services.AddScoped<IOutboxMessageHandler, TransactionRecordedHandler>();

            return services;
        }
    }
}
