using System.Linq.Expressions;

namespace BuildingBlocks.Application.Pagination
{
    public interface IKeysetPaginator
    {
        IQueryable<T> After<T, TKey, TTieBreaker>(
            IQueryable<T> source,
            Expression<Func<T, TKey>> key,
            Expression<Func<T, TTieBreaker>> tieBreaker,
            TKey lastKey,
            TTieBreaker lastTieBreaker,
            bool descending);
    }
}