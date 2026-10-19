using TransactionAggregation.Worker.Messaging;
using TransactionAggregation.Worker.Notifications;
using TransactionAggregation.Worker.Reporting;
using TransactionAggregation.Worker.Transactions;

namespace TransactionAggregation.Worker
{
    public static class BackgroundProcessing
    {
        public static IServiceCollection AddBackgroundProcessing(this IServiceCollection services, IConfiguration configuration) =>
            services
                .AddMessagingProcessing(configuration)
                .AddTransactionsProcessing(configuration)
                .AddReportingProcessing(configuration)
                .AddNotificationsProcessing();
    }
}
