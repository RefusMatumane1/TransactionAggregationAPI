namespace BuildingBlocks.Application.Pagination
{
    public readonly record struct BoundedCount(int Count, bool IsCapped)
    {
        public const int Limit = 10_000;

        // Counts at most Limit + 1 rows (COUNT over a LIMIT subquery), so the cost is bounded
        // however large the filtered set is.
        public static async Task<BoundedCount> OfAsync<T>(
            IQueryable<T> query, Func<IQueryable<T>, CancellationToken, Task<int>> countAsync, CancellationToken cancellationToken)
        {
            var counted = await countAsync(query.Take(Limit + 1), cancellationToken);
            return counted > Limit ? new BoundedCount(Limit, IsCapped: true) : new BoundedCount(counted, IsCapped: false);
        }
    }
}