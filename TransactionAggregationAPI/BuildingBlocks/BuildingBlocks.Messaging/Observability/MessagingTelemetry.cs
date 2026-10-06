using Prometheus;
using System.Diagnostics;

namespace BuildingBlocks.Messaging.Observability
{
    public static class MessagingTelemetry
    {
        public const string ActivitySourceName = "TransactionAggregation.Messaging";

        public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

        public const string CorrelationBaggageKey = "correlation.id";

        private static readonly double[] LatencyBuckets = [0.01, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60, 300, 900, 3600];

        public static readonly Gauge Backlog = Prometheus.Metrics.CreateGauge(
            "messaging_backlog_messages",
            "Inbox/outbox messages by queue and status (pending = waiting to be processed, dead_lettered = needs an operator).",
            new GaugeConfiguration { LabelNames = ["queue", "status"] });

        public static readonly Histogram ProcessingDuration = Prometheus.Metrics.CreateHistogram(
            "messaging_processing_duration_seconds",
            "Time to process one inbox/outbox message, by queue and outcome.",
            new HistogramConfiguration { LabelNames = ["queue", "outcome"], Buckets = LatencyBuckets });

        public static readonly Histogram EndToEndLag = Prometheus.Metrics.CreateHistogram(
            "messaging_end_to_end_lag_seconds",
            "Time from a message being written (received/occurred) to it being processed successfully, by queue.",
            new HistogramConfiguration { LabelNames = ["queue"], Buckets = LatencyBuckets });

        public static Activity? StartConsumerActivity(string name, string? traceParent) =>
            traceParent is not null && ActivityContext.TryParse(traceParent, null, out var parent)
                ? ActivitySource.StartActivity(name, ActivityKind.Consumer, parent)
                : ActivitySource.StartActivity(name, ActivityKind.Consumer);
    }
}