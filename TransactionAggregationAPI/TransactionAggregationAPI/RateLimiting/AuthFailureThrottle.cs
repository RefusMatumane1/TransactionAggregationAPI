using StackExchange.Redis;

namespace TransactionAggregationAPI.RateLimiting
{
    // Requests that fail authentication never reach the rate limiter (it runs after authorization),
    // so a client sending bad tokens or keys would otherwise be free to do so without limit.
    internal interface IAuthFailureThrottle
    {
        // How much longer the client is blocked for, or null when it may proceed.
        Task<TimeSpan?> BlockedForAsync(string client, CancellationToken cancellationToken);

        Task RecordFailureAsync(string client, CancellationToken cancellationToken);
    }

    internal sealed class RedisAuthFailureThrottle(
        IConnectionMultiplexer redis,
        ILogger<RedisAuthFailureThrottle> logger) : IAuthFailureThrottle
    {
        public const int FailureLimit = 60;
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

        private static readonly LuaScript CountFailure = LuaScript.Prepare("""
            local count = redis.call('INCR', @key)
            if count == 1 or redis.call('PTTL', @key) == -1 then
                redis.call('PEXPIRE', @key, @windowMs)
            end
            return count
            """);

        private static RedisKey KeyFor(string client) => $"ratelimit:authfail:{client}";

        public async Task<TimeSpan?> BlockedForAsync(string client, CancellationToken cancellationToken)
        {
            try
            {
                var db = redis.GetDatabase();
                var failures = await db.StringGetAsync(KeyFor(client));
                if (!failures.HasValue || (long)failures < FailureLimit)
                    return null;

                return await db.KeyTimeToLiveAsync(KeyFor(client)) ?? Window;
            }
            catch (RedisException ex)
            {
                logger.LogDebug(ex, "Authentication-failure throttle skipped: Redis is unreachable");
                return null;
            }
        }

        public async Task RecordFailureAsync(string client, CancellationToken cancellationToken)
        {
            try
            {
                await redis.GetDatabase().ScriptEvaluateAsync(
                    CountFailure, new { key = KeyFor(client), windowMs = (long)Window.TotalMilliseconds });
            }
            catch (RedisException ex)
            {
                logger.LogDebug(ex, "Authentication failure not counted: Redis is unreachable");
            }
        }
    }
}
