namespace BuildingBlocks.Application.Pagination
{
    public sealed record CursorPage<T>(
        IReadOnlyList<T> Items, int PageSize, string? NextCursor, int? TotalCount = null, bool TotalCountCapped = false)
    {
        public bool HasMore => NextCursor is not null;
    }
}