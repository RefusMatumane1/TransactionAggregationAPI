namespace TransactionAggregation.MockAggregator
{
    public sealed class MockAggregatorOptions
    {
        public const string SectionName = "MockAggregator";

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

        // Each bank's key is ApiKey + "-" + its code in lower case, as in the API's Development registration.
        public string KeyFor(string bankCode) => $"{ApiKey}-{bankCode.ToLowerInvariant()}";

        public string KafkaTopic { get; set; } = "bank-transactions";
    }

    public enum DeliveryChannel
    {
        Webhook,
        Kafka
    }
}