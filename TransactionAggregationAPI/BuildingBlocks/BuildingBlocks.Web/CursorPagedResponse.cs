using BuildingBlocks.Application.Pagination;

namespace BuildingBlocks.Web
{
    public sealed record CursorPagedResponse<T>(
        IReadOnlyList<T> Items,
        int PageSize,
        string? NextCursor,
        bool HasMore,
        int? TotalCount,
        bool TotalCountCapped)
    {
        public static CursorPagedResponse<T> From<TSource>(CursorPage<TSource> page, Func<TSource, T> map) =>
            new(page.Items.Select(map).ToList(), page.PageSize, page.NextCursor, page.HasMore, page.TotalCount, page.TotalCountCapped);
    }
}