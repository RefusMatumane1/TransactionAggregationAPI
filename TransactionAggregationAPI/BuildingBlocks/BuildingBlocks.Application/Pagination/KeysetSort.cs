using System.Linq.Expressions;

namespace BuildingBlocks.Application.Pagination
{
    public abstract class KeysetSort<T>(string name)
    {
        public string Name { get; } = name;

        public abstract IOrderedQueryable<T> Order(IQueryable<T> source, bool descending);

        public abstract bool IsValid(PageCursor cursor, bool descending);

        public abstract IQueryable<T> After(IQueryable<T> source, IKeysetPaginator paginator, PageCursor cursor);

        public abstract PageCursor CursorAfter(T item, bool descending);

        public CursorPage<TResult> ToPage<TResult>(
            IReadOnlyList<T> fetched, int pageSize, bool descending, Func<T, TResult> map, BoundedCount? total = null)
        {
            var hasMore = fetched.Count > pageSize;
            var items = hasMore ? fetched.Take(pageSize).ToList() : fetched;
            var nextCursor = hasMore ? CursorAfter(items[^1], descending).Encode() : null;
            return new CursorPage<TResult>(
                items.Select(map).ToList(), pageSize, nextCursor, total?.Count, total?.IsCapped ?? false);
        }
    }

    public sealed class KeysetSort<T, TKey, TTieBreaker>(
        string name,
        Expression<Func<T, TKey>> key,
        Expression<Func<T, TTieBreaker>> tieBreaker,
        Func<TTieBreaker, Guid> tieBreakerToGuid,
        Func<Guid, TTieBreaker> guidToTieBreaker,
        KeyCodec<TKey> codec) : KeysetSort<T>(name)
    {
        private readonly Func<T, TKey> _readKey = key.Compile();
        private readonly Func<T, TTieBreaker> _readTieBreaker = tieBreaker.Compile();

        public override IOrderedQueryable<T> Order(IQueryable<T> source, bool descending) =>
            descending
                ? source.OrderByDescending(key).ThenByDescending(tieBreaker)
                : source.OrderBy(key).ThenBy(tieBreaker);

        public override bool IsValid(PageCursor cursor, bool descending) =>
            string.Equals(cursor.Sort, Name, StringComparison.Ordinal)
            && cursor.Descending == descending
            && codec.TryParse(cursor.Key, out _);

        public override IQueryable<T> After(IQueryable<T> source, IKeysetPaginator paginator, PageCursor cursor)
        {
            if (!codec.TryParse(cursor.Key, out var lastKey))
                throw new ArgumentException($"Cursor key is not valid for sort '{Name}'.", nameof(cursor));

            return paginator.After(source, key, tieBreaker, lastKey, guidToTieBreaker(cursor.Id), cursor.Descending);
        }

        public override PageCursor CursorAfter(T item, bool descending) =>
            new(Name, descending, codec.Format(_readKey(item)), tieBreakerToGuid(_readTieBreaker(item)));
    }
}