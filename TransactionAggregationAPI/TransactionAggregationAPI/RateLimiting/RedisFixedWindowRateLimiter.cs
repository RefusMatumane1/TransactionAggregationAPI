using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Threading.RateLimiting;

namespace TransactionAggregationAPI.RateLimiting;

internal class RedisFixedWindowRateLimiter : RateLimiter
{
    private readonly IDatabase _db;
    private readonly string _partitionKey;
    private readonly RedisRateLimiterOptions _options;
    private readonly ILogger? _logger;

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
        RedisRateLimiterOptions options,
        ILogger? logger = null)
    {
        _db = redis.GetDatabase();
        _partitionKey = partitionKey;
        _options = options;
        _logger = logger;
    }

    public override RateLimiterStatistics? GetStatistics()
    {
        throw new NotImplementedException();
    }

    public override TimeSpan? IdleDuration => null;

    public int GetAvailablePermits() => _options.PermitLimit;

    protected override async ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
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
        catch (RedisException ex)
        {
            if (_options.AllowRequestOnRedisFailure)
            {
                _logger?.LogWarning(ex,
                    "Rate limiter failing open for partition {PartitionKey} — Redis is unreachable, so this request is NOT being rate limited",
                    _partitionKey);
                return SuccessfulLease.Instance;
            }

            return new FailedLease(_options.Window);
        }
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
        catch (RedisException ex)
        {
            if (_options.AllowRequestOnRedisFailure)
            {
                _logger?.LogWarning(ex,
                    "Rate limiter failing open for partition {PartitionKey} — Redis is unreachable, so this request is NOT being rate limited",
                    _partitionKey);
                return SuccessfulLease.Instance;
            }

            return new FailedLease(_options.Window);
        }
    }

    protected override void Dispose(bool disposing) { }
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
    protected override void Dispose(bool disposing) { }
}

file sealed class FailedLease : RateLimitLease
{
    private readonly TimeSpan _retryAfter;

    public FailedLease(TimeSpan retryAfter) => _retryAfter = retryAfter;

    public override bool IsAcquired => false;

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