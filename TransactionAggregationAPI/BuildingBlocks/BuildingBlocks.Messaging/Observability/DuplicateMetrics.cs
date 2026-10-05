using Prometheus;

namespace BuildingBlocks.Messaging.Observability
{
    public static class DuplicateMetrics
    {
        public const string MessageLevel = "message";
        public const string TransactionLevel = "transaction";

        public static readonly Counter InboundDuplicates = Prometheus.Metrics.CreateCounter(
            "inbound_duplicates_total",
            "Inbound deliveries (level=message) or individual transactions (level=transaction) dropped as duplicates.",
            new CounterConfiguration { LabelNames = ["source_name", "level"] });
    }
}