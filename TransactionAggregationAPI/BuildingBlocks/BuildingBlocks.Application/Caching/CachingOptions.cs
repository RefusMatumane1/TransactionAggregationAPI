namespace BuildingBlocks.Application.Caching
{
    public sealed class CachingOptions
    {
        public const string SectionName = "Caching";

        // Per-scope lifetime in minutes (e.g. "transaction-aggregates": 55); capped by CachingBehavior.MaxExpiration.
        public Dictionary<string, int> ScopeExpirationMinutes { get; set; } = new(StringComparer.Ordinal);
    }
}