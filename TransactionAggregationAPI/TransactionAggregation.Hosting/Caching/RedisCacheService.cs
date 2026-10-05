using BuildingBlocks.Application.Caching;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace TransactionAggregation.Hosting.Caching
{
    public class RedisCacheService : ICacheService
    {
        public static readonly TimeSpan ScopeVersionTtl = TimeSpan.FromDays(1);
        private static readonly TimeSpan DefaultEntryTtl = TimeSpan.FromHours(1);

        private readonly IDatabase _database;
        private readonly ILogger<RedisCacheService> _logger;

        public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
        {
            _database = redis.GetDatabase();
            _logger = logger;
        }

        public static string ScopeVersionKey(string scope) => $"cachever:{scope}";

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                var cachedData = await _database.StringGetAsync(key);
                if (!cachedData.HasValue)
                    return null;

                return JsonSerializer.Deserialize<T>((byte[])cachedData!);
            }
            catch (Exception ex) when (ex is RedisException or JsonException)
            {
                _logger.LogWarning(ex, "Cache read failed for key {Key} — treating as a miss", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                await _database.StringSetAsync(key, JsonSerializer.SerializeToUtf8Bytes(value), expiration ?? DefaultEntryTtl);
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Cache write failed for key {Key}", key);
            }
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                await _database.KeyDeleteAsync(key);
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Cache delete failed for key {Key}", key);
            }
        }

        public async Task<long?> GetScopeVersionAsync(string scope, CancellationToken cancellationToken = default)
        {
            try
            {
                var value = await _database.StringGetAsync(ScopeVersionKey(scope));
                return value.HasValue && long.TryParse(value.ToString(), out var version) ? version : 0;
            }
            catch (RedisException ex)
            {
                _logger.LogWarning(ex, "Cache scope version unavailable for {CacheScope} — bypassing the cache", scope);
                return null;
            }
        }

        public async Task InvalidateScopeAsync(string scope, CancellationToken cancellationToken = default)
        {
            var key = ScopeVersionKey(scope);
            var batch = _database.CreateTransaction();
            var increment = batch.StringIncrementAsync(key);
            var expire = batch.KeyExpireAsync(key, ScopeVersionTtl);
            await batch.ExecuteAsync();
            await Task.WhenAll(increment, expire);
        }
    }
}