
namespace Modules.Reporting.Application.Aggregation
{
    public static class ReportingCacheScopes
    {
        // Same value the aggregates were cached under before Reporting became its own module, so a rolling
        // deploy never leaves old and new replicas invalidating different keys.
        public const string Aggregates = "transaction-aggregates";

        public static readonly TimeSpan AggregatesExpiration = TimeSpan.FromMinutes(55);
    }
}
