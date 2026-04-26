
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using TransactionAggregation.Application.Common.Interfaces;

namespace TransactionAggregation.Infrastructure.Services
{
    public class RedisCacheService : ICacheService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _database;
        private readonly ILogger<RedisCacheService> _logger;

        public RedisCacheService(
            IConnectionMultiplexer redis,
            ILogger<RedisCacheService> logger)
        {
            _redis = redis;
            _database = redis.GetDatabase();
            _logger = logger;
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                var cachedData = await _database.StringGetAsync(key);
                if (!cachedData.HasValue)
                    return null;

                return JsonSerializer.Deserialize<T>(new MemoryStream(cachedData));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cached data for key {Key}", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                var serializedData = JsonSerializer.Serialize(value);
                await _database.StringSetAsync(
                    key,
                    serializedData,
                    expiration ?? TimeSpan.FromHours(1));

                _logger.LogDebug("Cached data for key {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cached data for key {Key}", key);
            }
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                await _database.KeyDeleteAsync(key);
                _logger.LogDebug("Removed cached data for key {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cached data for key {Key}", key);
            }
        }

        public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            try
            {
                var endpoints = _redis.GetEndPoints();
                if (endpoints.Length == 0) return;

                var server = _redis.GetServer(endpoints[0]);

                // SCAN for keys matching pattern (more efficient than KEYS)
                await foreach (var key in server.KeysAsync(pattern: pattern, pageSize: 100))
                {
                    await _database.KeyDeleteAsync(key);
                }

                _logger.LogDebug("Removed cached data for pattern {Pattern}", pattern);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cached data for pattern {Pattern}", pattern);
            }
        }
    }
}
