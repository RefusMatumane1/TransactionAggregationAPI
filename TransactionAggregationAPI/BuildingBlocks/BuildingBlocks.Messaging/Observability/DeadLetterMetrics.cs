using Prometheus;

namespace BuildingBlocks.Messaging.Observability
{
    public static class DeadLetterMetrics
    {
        public static readonly Counter InboxMessagesDeadLettered = Prometheus.Metrics.CreateCounter(
            "inbox_messages_dead_lettered_total",
            "Total number of inbox messages that exhausted their retry budget and were dead-lettered.",
            new CounterConfiguration { LabelNames = ["source_name"] });

        public static readonly Counter OutboxMessagesDeadLettered = Prometheus.Metrics.CreateCounter(
            "outbox_messages_dead_lettered_total",
            "Total number of outbox messages that exhausted their retry budget and were dead-lettered.",
            new CounterConfiguration { LabelNames = ["message_type"] });
    }
}