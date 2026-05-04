namespace TransactionAggregationAPI.RateLimiting;

internal sealed class RedisRateLimiterOptions
{
    public required int PermitLimit { get; init; }
    public required TimeSpan Window { get; init; }

    // When Redis is unreachable, allow the request rather than returning 429.
    // Prefer leniency over a total outage caused by a cache dependency.
    public bool AllowRequestOnRedisFailure { get; init; } = true;
}
