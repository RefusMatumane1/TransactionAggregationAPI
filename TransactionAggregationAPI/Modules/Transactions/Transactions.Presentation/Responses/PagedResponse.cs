using Modules.Transactions.Application.Common.Models;

namespace Modules.Transactions.Presentation.Responses
{
    /// <summary>The offset-pagination envelope (ADR-0007) around a page of response items.</summary>
    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int PageNumber,
        int PageSize,
        int TotalCount,
        int TotalPages,
        bool HasPreviousPage,
        bool HasNextPage,
        DateTime GeneratedAt,
        string? NextPageUrl,
        string? PreviousPageUrl);

    internal static class PagedResponse
    {
        public static PagedResponse<TResponse> From<TSource, TResponse>(
            PaginatedResponse<TSource> page, Func<TSource, TResponse> toResponse) => new(
            page.Items.Select(toResponse).ToList(),
            page.PageNumber,
            page.PageSize,
            page.TotalCount,
            page.TotalPages,
            page.HasPreviousPage,
            page.HasNextPage,
            page.GeneratedAt,
            page.NextPageUrl,
            page.PreviousPageUrl);
    }
}