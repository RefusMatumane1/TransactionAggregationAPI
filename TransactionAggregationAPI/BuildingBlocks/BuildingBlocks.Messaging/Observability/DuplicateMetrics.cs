using Prometheus;

namespace BuildingBlocks.Messaging.Observability
{
    /// <summary>
    /// Duplicates are expected under at-least-once delivery (webhook retries, Kafka
    /// redelivery after a rebalance) and are dropped silently by design — this counter is
    /// what makes a sudden spike, e.g. a misbehaving producer replaying a whole topic,
    /// visible to an alert.
    /// </summary>
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