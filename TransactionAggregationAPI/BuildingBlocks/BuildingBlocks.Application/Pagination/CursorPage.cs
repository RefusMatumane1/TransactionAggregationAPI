namespace BuildingBlocks.Application.Pagination
{
    // TotalCount is counted up to BoundedCount.Limit; TotalCountCapped means "at least that many",
    // so asking for a total never costs a scan of the whole table.
    public sealed record CursorPage<T>(
        IReadOnlyList<T> Items, int PageSize, string? NextCursor, int? TotalCount = null, bool TotalCountCapped = false)
    {
        public bool HasMore => NextCursor is not null;
    }
}