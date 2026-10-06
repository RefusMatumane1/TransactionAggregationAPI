using TransactionAggregationUI.Models.Shared;
using TransactionAggregationUI.Models.Transactions;

namespace TransactionAggregationUI.Services
{
    public class TransactionService(ApiClient api)
    {
        private const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss";

        public Task<(CursorPage<TransactionModel>? Value, string? Error)> FilterTransactionsAsync(TransactionFilterParams f)
        {
            var query = ApiQuery.Of(
                ("cursor", f.Cursor),
                ("includeTotal", f.Cursor is null ? true : null),
                ("pageSize", f.PageSize),
                ("sortDescending", f.SortDescending),
                ("category", f.Category),
                ("fromDate", f.FromDate?.ToString(DateTimeFormat)),
                ("toDate", f.ToDate?.ToString(DateTimeFormat)),
                ("minAmount", f.MinAmount),
                ("maxAmount", f.MaxAmount),
                ("searchTerm", f.SearchTerm),
                ("institution", f.Institution),
                ("externalAccountId", f.ExternalAccountId),
                ("sortBy", f.SortBy));

            var path = f.CustomerId is { } customer ? $"api/v1/customers/{customer}/transactions" : "api/v1/transactions";
            return api.GetAsync<CursorPage<TransactionModel>>($"{path}?{query}");
        }

        public Task<(TransactionDetailModel? Value, string? Error)> GetAsync(Guid id) =>
            api.GetAsync<TransactionDetailModel>($"api/v1/transactions/{id}",
                notFoundMessage: "This transaction no longer exists or you can't see its bank.");
    }
}