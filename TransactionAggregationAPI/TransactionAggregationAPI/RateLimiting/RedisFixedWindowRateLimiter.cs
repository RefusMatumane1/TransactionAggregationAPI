using StackExchange.Redis;
using System.Threading.RateLimiting;

namespace TransactionAggregationAPI.RateLimiting
{
    // A fixed window counted in Redis, so every API replica shares one limit per caller.
    internal sealed class RedisFixedWindowRateLimiter(
        IConnectionMultiplexer redis,
        string scope,
        string partitionKey,
        RedisRateLimiterOptions options,
        ILogger logger,
        TimeProvider? timeProvider = null) : RateLimiter
    {
        private static readonly TimeSpan OutageWarningInterval = TimeSpan.FromSeconds(30);
        private static long _lastOutageWarningTimestamp;

        private readonly IDatabase _db = redis.GetDatabase();
        private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
        private readonly string _key = $"ratelimit:{scope}:{partitionKey}";
        private long _lastUsedTimestamp = (timeProvider ?? TimeProvider.System).GetTimestamp();

        private static readonly LuaScript AtomicIncrScript = LuaScript.Prepare("""
            local count = redis.call('INCR', @key)
            if count == 1 then
                redis.call('PEXPIRE', @key, @windowMs)
            elseif redis.call('PTTL', @key) == -1 then
                -- Recovery: key exists but has no expiry (previous PEXPIRE failed).
                redis.call('PEXPIRE', @key, @windowMs)
            end
            return {count, redis.call('PTTL', @key)}
            """);

        private object ScriptArguments => new
        {
            key = (RedisKey)_key,
            windowMs = (long)options.Window.TotalMilliseconds
        };

        public override TimeSpan? IdleDuration => _time.GetElapsedTime(Interlocked.Read(ref _lastUsedTimestamp));

        public override RateLimiterStatistics? GetStatistics() => null;

        protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
        {
            Interlocked.Exchange(ref _lastUsedTimestamp, _time.GetTimestamp());
            try
            {
                return LeaseFor(await _db.ScriptEvaluateAsync(AtomicIncrScript, ScriptArguments));
            }
            catch (RedisException ex)
            {
                return OnRedisUnavailable(ex);
            }
        }

        // The rate-limiting middleware tries AttemptAcquire first and falls back to AcquireAsync when
        // it is not granted. Answering "not acquired" here without touching Redis makes every request
        // take the asynchronous path: one counted increment, and no thread blocked on network I/O.
        protected override RateLimitLease AttemptAcquireCore(int permitCount) => DeferredLease.Instance;

        private RateLimitLease LeaseFor(RedisResult result)
        {
            var values = (RedisResult[])result!;
            var requestsInWindow = (long)values[0];
            if (requestsInWindow <= options.PermitLimit)
                return SuccessfulLease.Instance;

            var remainingMs = (long)values[1];
            return new FailedLease(remainingMs > 0 ? TimeSpan.FromMilliseconds(remainingMs) : options.Window);
        }

        private RateLimitLease OnRedisUnavailable(RedisException ex)
        {
            if (!options.AllowRequestOnRedisFailure)
                return new FailedLease(options.Window);

            var now = _time.GetTimestamp();
            var last = Interlocked.Read(ref _lastOutageWarningTimestamp);
            if (last == 0 || _time.GetElapsedTime(last, now) >= OutageWarningInterval)
            {
                Interlocked.Exchange(ref _lastOutageWarningTimestamp, now);
                logger.LogWarning(ex,
                    "Rate limiter ({Scope}) failing open: Redis is unreachable, so requests are not being rate limited",
                    scope);
            }

            return SuccessfulLease.Instance;
        }
    }

    file sealed class SuccessfulLease : RateLimitLease
    {
        public static readonly SuccessfulLease Instance = new();

        public override bool IsAcquired => true;
        public override IEnumerable<string> MetadataNames => [];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }
    }

    file sealed class DeferredLease : RateLimitLease
    {
        public static readonly DeferredLease Instance = new();

        public override bool IsAcquired => false;
        public override IEnumerable<string> MetadataNames => [];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }
    }

    file sealed class FailedLease(TimeSpan retryAfter) : RateLimitLease
    {
        public override bool IsAcquired => false;
        public override IEnumerable<string> MetadataNames => [MetadataName.RetryAfter.Name];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = metadataName == MetadataName.RetryAfter.Name ? retryAfter : null;
            return metadata is not null;
        }
    }
}