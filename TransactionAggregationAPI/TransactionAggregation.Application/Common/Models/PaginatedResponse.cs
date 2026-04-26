namespace TransactionAggregation.Application.Common.Models
{
    public sealed class PaginatedResponse<T>
    {
        public IReadOnlyList<T> Items { get; init; } = new List<T>();
        public int PageNumber { get; init; }
        public int PageSize { get; init; }
        public int TotalCount { get; init; }
        public int TotalPages { get; init; }
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;

        // Metadata
        public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
        public string? NextPageUrl { get; init; }
        public string? PreviousPageUrl { get; init; }

        public static PaginatedResponse<T> Create(
            IReadOnlyList<T> items,
            int totalCount,
            int pageNumber,
            int pageSize,
            string? baseUrl = null)
        {
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return new PaginatedResponse<T>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                NextPageUrl = pageNumber < totalPages && baseUrl != null
                    ? $"{baseUrl}?pageNumber={pageNumber + 1}&pageSize={pageSize}"
                    : null,
                PreviousPageUrl = pageNumber > 1 && baseUrl != null
                    ? $"{baseUrl}?pageNumber={pageNumber - 1}&pageSize={pageSize}"
                    : null
            };
        }
    }
}
