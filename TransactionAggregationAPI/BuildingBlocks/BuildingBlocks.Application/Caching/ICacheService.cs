namespace BuildingBlocks.Application.Caching
{
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);

        Task<long?> GetScopeVersionAsync(string scope, CancellationToken cancellationToken = default);

        Task InvalidateScopeAsync(string scope, CancellationToken cancellationToken = default);
    }
}