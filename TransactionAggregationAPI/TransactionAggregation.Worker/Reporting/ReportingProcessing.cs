using TransactionAggregation.Worker.Messaging;

namespace TransactionAggregation.Worker.Reporting
{
    internal static class ReportingProcessing
    {
        /// <summary>Keeps the daily-totals read model current.</summary>
        public static IServiceCollection AddReportingProcessing(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<AggregationOptions>(configuration.GetSection(AggregationOptions.SectionName));
            services.AddHostedServiceIfEnabled<DailyTotalsRefreshBackgroundService>(configuration, AggregationOptions.SectionName);

            return services;
        }
    }
}
