namespace TransactionAggregation.Worker.Kafka
{
    public sealed class KafkaOptions
    {
        public const string SectionName = "Kafka";

        public const string ConnectionStringName = "kafka";

        public bool Enabled { get; set; } = true;

        public string BankTransactionsTopic { get; set; } = "bank-transactions";

        public string DeadLetterTopic { get; set; } = "bank-transactions.dlq";

        // Outbound integration events (Transactions.Contracts), published by the outbox dispatcher.
        public string IntegrationEventsTopic { get; set; } = "transaction-events";

        public string GroupId { get; set; } = "transaction-aggregation-api";

        public bool CreateTopics { get; set; } = true;

        public int TopicPartitions { get; set; } = 3;

        public short TopicReplicationFactor { get; set; } = 1;

        public int MaxRetryBackoffSeconds { get; set; } = 60;

        public int MaxUnclassifiedAttempts { get; set; } = 5;
    }
}