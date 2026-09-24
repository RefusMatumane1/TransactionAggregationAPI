using Prometheus;

namespace Modules.Transactions.Infrastructure.Kafka
{
    internal static class KafkaMetrics
    {
        public static readonly Counter MessagesConsumed = Prometheus.Metrics.CreateCounter(
            "kafka_bank_transactions_messages_total",
            "Kafka records consumed from the bank-transactions topic, by outcome (enqueued, duplicate, dead_lettered).",
            new CounterConfiguration { LabelNames = ["outcome"] });

        public static readonly Counter TransientFailures = Prometheus.Metrics.CreateCounter(
            "kafka_bank_transactions_transient_failures_total",
            "Attempts to hand a Kafka record to the inbox that failed transiently and will be retried.");

        public static readonly Counter ConsumerCrashes = Prometheus.Metrics.CreateCounter(
            "kafka_bank_transactions_consumer_crashes_total",
            "Times the bank-transactions consumer loop died unexpectedly and stopped its host for a restart.");
    }
}