using TransactionAggregation.Application.Common.Interfaces;

namespace TransactionAggregation.Tests.Helpers;

public sealed class NoOpCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        => Task.FromResult<T?>(null);

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
        => Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}