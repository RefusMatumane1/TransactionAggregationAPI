namespace Modules.Transactions.Infrastructure.Kafka
{
    public sealed class KafkaOptions
    {
        public const string SectionName = "Kafka";

        /// <summary>
        /// Bootstrap servers come from ConnectionStrings:kafka (what Aspire's WithReference
        /// injects); the consumer isn't registered at all when it's empty.
        /// </summary>
        public const string ConnectionStringName = "kafka";

        public bool Enabled { get; set; } = true;

        public string BankTransactionsTopic { get; set; } = "bank-transactions";

        /// <summary>Messages that can never succeed (malformed JSON, failed validation) are parked here.</summary>
        public string DeadLetterTopic { get; set; } = "bank-transactions.dlq";

        public string GroupId { get; set; } = "transaction-aggregation-api";

        /// <summary>
        /// The SourceName for records that carry no "source" header. SourceName scopes idempotency
        /// keys, so Kafka and webhook deliveries of the same batch are deduplicated separately at the
        /// delivery level, and together at the transaction level. Unlike a header value, this default
        /// is trusted as configured and not checked against the WebhookSources registry.
        /// </summary>
        public string SourceName { get; set; } = "kafka-bank-aggregator";

        /// <summary>
        /// When true, a record without a "source" header is dead-lettered instead of falling
        /// back to <see cref="SourceName"/> — turn on once every producer sends the header.
        /// </summary>
        public bool RequireSourceHeader { get; set; }

        /// <summary>Creates both topics on startup if missing — turn off where topics are provisioned elsewhere.</summary>
        public bool CreateTopics { get; set; } = true;

        public int TopicPartitions { get; set; } = 3;

        public short TopicReplicationFactor { get; set; } = 1;

        /// <summary>Cap for the exponential backoff while a transient failure (e.g. Postgres down) blocks a partition.</summary>
        public int MaxRetryBackoffSeconds { get; set; } = 60;
    }
}