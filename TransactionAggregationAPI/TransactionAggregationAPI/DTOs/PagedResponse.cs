using TransactionAggregation.Application.Common.Models;

namespace TransactionAggregationAPI.DTOs
{
    public sealed record PagedResponse<T>(
        IEnumerable<T> Items,
        int TotalCount,
        int CurrentPage,
        int PageSize,
        int TotalPages,
        bool HasNextPage,
        bool HasPreviousPage)
    {
        public static PagedResponse<T> From(PagedResult<T> result)
        {
            return new PagedResponse<T>(
                result.Items,
                result.TotalCount,
                result.CurrentPage,
                result.PageSize,
                result.TotalPages,
                result.HasNextPage,
                result.HasPreviousPage);
        }
    }
}
