namespace BuildingBlocks.Application.Caching
{
    public sealed class CachingOptions
    {
        public const string SectionName = "Caching";

        // Per-scope entry lifetime in minutes, overriding the query's own CacheExpiration
        // (e.g. "transaction-aggregates": 55). Still capped by CachingBehavior.MaxExpiration.
        public Dictionary<string, int> ScopeExpirationMinutes { get; set; } = new(StringComparer.Ordinal);
    }
}