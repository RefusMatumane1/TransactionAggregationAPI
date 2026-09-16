
namespace TransactionAggregation.Application.Common.Models
{
    public record PagedResult<T>(
    IEnumerable<T> Items,
    int TotalCount,
    int CurrentPage,
    int PageSize)
    {
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasNextPage => CurrentPage < TotalPages;
        public bool HasPreviousPage => CurrentPage > 1;
    }
}