using StackExchange.Redis;
using System.Threading.RateLimiting;

namespace TransactionAggregationAPI.RateLimiting;

/// <summary>
/// A fixed-window rate limiter backed by Redis.
/// All counter state lives in Redis, so the limit is enforced consistently
/// across every replica in the cluster.
///
/// Algorithm: atomic INCR + PEXPIRE via Lua script.
/// The script increments a key and sets its TTL on first access (count == 1).
/// A second PTTL guard covers the rare case where a key was created without
/// an expiry due to a previous failed PEXPIRE call.
/// </summary>
internal class RedisFixedWindowRateLimiter : RateLimiter
{
    private readonly IDatabase _db;
    private readonly string _partitionKey;
    private readonly RedisRateLimiterOptions _options;

    // Prepared script is compiled once and SHA-cached by StackExchange.Redis.
    // Redis executes it atomically; no MULTI/EXEC needed.
    private static readonly LuaScript AtomicIncrScript = LuaScript.Prepare("""
        local count = redis.call('INCR', @key)
        if count == 1 then
            redis.call('PEXPIRE', @key, @windowMs)
        elseif redis.call('PTTL', @key) == -1 then
            -- Recovery: key exists but has no expiry (previous PEXPIRE failed).
            redis.call('PEXPIRE', @key, @windowMs)
        end
        return count
        """);

    public RedisFixedWindowRateLimiter(
        IConnectionMultiplexer redis,
        string partitionKey,
        RedisRateLimiterOptions options)
    {
        _db = redis.GetDatabase();
        _partitionKey = partitionKey;
        _options = options;
    }

    public override RateLimiterStatistics? GetStatistics()
    {
        throw new NotImplementedException();
    }

    public override TimeSpan? IdleDuration => null;

    // GetAvailablePermits is a best-effort hint; the real value is in Redis.
    public int GetAvailablePermits() => _options.PermitLimit;

    // Synchronous path — used when callers invoke Acquire() instead of AcquireAsync().
    // StackExchange.Redis has a synchronous ScriptEvaluate overload so we avoid
    // blocking an async thread via .GetAwaiter().GetResult().
    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
      return ValueTask.FromResult(AttemptAcquireCore(permitCount));
    }

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        try
        {
            var result = (long)_db.ScriptEvaluate(AtomicIncrScript, new
            {
                key = (RedisKey)_partitionKey,
                windowMs = (long)_options.Window.TotalMilliseconds
            });

            return result <= _options.PermitLimit
                ? SuccessfulLease.Instance
                : new FailedLease(_options.Window);
        }
        catch (RedisException)
        {
            return _options.AllowRequestOnRedisFailure
                ? SuccessfulLease.Instance
                : new FailedLease(_options.Window);
        }
    }

    // Async path — called by the ASP.NET Core RateLimitingMiddleware.
    protected virtual async ValueTask<RateLimitLease> WaitAndAcquireAsyncCore(
        int permitCount,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _db.ScriptEvaluateAsync(AtomicIncrScript, new
            {
                key = (RedisKey)_partitionKey,
                windowMs = (long)_options.Window.TotalMilliseconds
            });

            return (long)result <= _options.PermitLimit
                ? SuccessfulLease.Instance
                : new FailedLease(_options.Window);
        }
        catch (RedisException)
        {
            return _options.AllowRequestOnRedisFailure
                ? SuccessfulLease.Instance
                : new FailedLease(_options.Window);
        }
    }

    protected override void Dispose(bool disposing) { }
}

// ── Lease implementations ────────────────────────────────────────────────────

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
    protected override void Dispose(bool disposing) { }
}

file sealed class FailedLease : RateLimitLease
{
    private readonly TimeSpan _retryAfter;

    public FailedLease(TimeSpan retryAfter) => _retryAfter = retryAfter;

    public override bool IsAcquired => false;

    // Surfacing RetryAfter lets the RateLimitingMiddleware add a Retry-After
    // header to the 429 response automatically.
    public override IEnumerable<string> MetadataNames =>
        ["retry-after"];

    public override bool TryGetMetadata(string metadataName, out object? metadata)
    {
        if (metadataName == "retry-after")
        {
            metadata = _retryAfter;
            return true;
        }
        metadata = null;
        return false;
    }

    protected override void Dispose(bool disposing) { }
}
