namespace BuildingBlocks.Application.Pagination
{
    // TotalCountCapped means "at least TotalCount": counting stops at BoundedCount.Limit.
    public sealed record CursorPage<T>(
        IReadOnlyList<T> Items, int PageSize, string? NextCursor, int? TotalCount = null, bool TotalCountCapped = false)
    {
        public bool HasMore => NextCursor is not null;
    }
}