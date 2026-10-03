namespace TransactionAggregation.MockAggregator
{
    public sealed class MockAggregatorOptions
    {
        public const string SectionName = "MockAggregator";

        // Where the Kafka record-signing key is persisted between runs.
        public string DataDirectory { get; set; } = "App_Data";
    }

    public sealed class FeedOptions
    {
        public const string SectionName = "Feed";

        public bool Enabled { get; set; } = true;

        public DeliveryChannel Channel { get; set; } = DeliveryChannel.Webhook;

        public int IntervalSeconds { get; set; } = 30;

        public int MaxTransactionsPerAccount { get; set; } = 3;

        public double PendingShare { get; set; } = 0.3;

        public double RedeliveryShare { get; set; } = 0.05;

        public string ApiBaseUrl { get; set; } = string.Empty;

        public string ApiKey { get; set; } = string.Empty;

        // Each bank is its own source with its own key: the key for a bank is ApiKey + "-" + the
        // bank's code in lower case, the convention the API's Development registration uses too.
        public string KeyFor(string bankCode) => $"{ApiKey}-{bankCode.ToLowerInvariant()}";

        public string KafkaTopic { get; set; } = "bank-transactions";
    }

    public enum DeliveryChannel
    {
        Webhook,
        Kafka
    }
}