namespace TransactionAggregation.MockAggregator;

/// <summary>The consent (OAuth) side: who may link accounts, and where consents are kept.</summary>
public sealed class MockAggregatorOptions
{
    public const string SectionName = "MockAggregator";

    /// <summary>Must match the application's BankAggregator:ClientId.</summary>
    public string ClientId { get; set; } = "transaction-aggregation-dev";

    /// <summary>Must match the application's BankAggregator:ClientSecret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Exact redirect URIs the consent page may send the browser back to. Checked even in a
    /// mock: an unchecked redirect_uri is an open redirect.
    /// </summary>
    public List<string> AllowedRedirectUris { get; set; } = new();

    /// <summary>Where granted consents are persisted, so the feed survives a restart.</summary>
    public string DataDirectory { get; set; } = "App_Data";
}

/// <summary>The push side: how often, how much, and over which channel.</summary>
public sealed class FeedOptions
{
    public const string SectionName = "Feed";

    public bool Enabled { get; set; } = true;

    public DeliveryChannel Channel { get; set; } = DeliveryChannel.Webhook;

    public int IntervalSeconds { get; set; } = 30;

    /// <summary>Upper bound on new transactions per consented account per tick (0..N).</summary>
    public int MaxTransactionsPerAccount { get; set; } = 3;

    /// <summary>Share of card purchases first sent as pending and posted on a later tick.</summary>
    public double PendingShare { get; set; } = 0.3;

    /// <summary>Share of ticks that re-send an account's previous batch unchanged, as a flaky sender would.</summary>
    public double RedeliveryShare { get; set; } = 0.05;

    /// <summary>Base URL of the application API, for the webhook channel.</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>The webhook source's API key (X-Api-Key).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>The webhook source name; also sent as the Kafka "source" header.</summary>
    public string SourceName { get; set; } = "mock-aggregator";

    public string KafkaTopic { get; set; } = "bank-transactions";
}

public enum DeliveryChannel
{
    Webhook,
    Kafka
}