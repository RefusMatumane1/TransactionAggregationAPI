namespace BuildingBlocks.Application.Caching
{
    public interface ICacheableQuery
    {
        TimeSpan? CacheExpiration { get; }

        string CacheScope { get; }
    }
}