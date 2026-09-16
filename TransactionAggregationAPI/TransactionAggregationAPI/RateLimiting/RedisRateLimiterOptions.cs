namespace TransactionAggregationAPI.RateLimiting;

internal sealed class RedisRateLimiterOptions
{
    public required int PermitLimit { get; init; }
    public required TimeSpan Window { get; init; }

    public bool AllowRequestOnRedisFailure { get; init; } = true;
}