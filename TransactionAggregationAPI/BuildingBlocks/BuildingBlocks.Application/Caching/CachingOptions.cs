namespace BuildingBlocks.Application.Caching
{
    public sealed class CachingOptions
    {
        public const string SectionName = "Caching";

        public Dictionary<string, int> ScopeExpirationMinutes { get; set; } = new(StringComparer.Ordinal);
    }
}