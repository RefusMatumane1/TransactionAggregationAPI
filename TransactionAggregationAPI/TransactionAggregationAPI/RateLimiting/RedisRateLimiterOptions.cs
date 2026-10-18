namespace TransactionAggregationAPI.RateLimiting
{
    internal sealed class RedisRateLimiterOptions
    {
        public required int PermitLimit { get; init; }
        public required TimeSpan Window { get; init; }

        // When Redis is unreachable: true keeps serving under a per-instance limit, false rejects every request.
        public bool AllowRequestOnRedisFailure { get; init; } = true;
    }
}