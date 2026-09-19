using Prometheus;

namespace TransactionAggregation.Infrastructure.Observability
{
    /// <summary>
    /// Instructions.md section 24 requires failures to be observable; failure-scenarios.md
    /// scenario 17 (poison messages) flagged that dead-lettering a message was previously
    /// only visible as a log line — an accumulating pile of poison messages had no metric
    /// an alert could fire on. These counters close that gap.
    /// </summary>
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
