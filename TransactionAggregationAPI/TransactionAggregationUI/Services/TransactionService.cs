using System.Net.Http.Json;
using System.Text;
using TransactionAggregationUI.Models.Shared;
using TransactionAggregationUI.Models.Transactions;

namespace TransactionAggregationUI.Services;

public class TransactionService
{
    private readonly IHttpClientFactory _factory;

    public TransactionService(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Client => _factory.CreateClient("api");

    public async Task<PagedResponse<TransactionModel>?> FilterTransactionsAsync(
        Guid customerId, TransactionFilterParams filters)
    {
        try
        {
            var qs = BuildFilterQueryString(filters);
            return await Client.GetFromJsonAsync<PagedResponse<TransactionModel>>(
                $"api/v1/customers/{customerId}/transactions/filter?{qs}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<TransactionSummaryModel?> GetSummaryAsync(
        Guid customerId, DateTime? startDate = null, DateTime? endDate = null)
    {
        try
        {
            var qs = new StringBuilder();
            if (startDate.HasValue)
                qs.Append($"startDate={startDate.Value:yyyy-MM-dd}&");
            if (endDate.HasValue)
                qs.Append($"endDate={endDate.Value:yyyy-MM-dd}&");

            return await Client.GetFromJsonAsync<TransactionSummaryModel>(
                $"api/v1/customers/{customerId}/transactions/summary?{qs}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool success, string? error)> CreateTransactionAsync(
        Guid customerId, CreateTransactionModel model)
    {
        try
        {
            var response = await Client.PostAsJsonAsync(
                $"api/v1/customers/{customerId}/transactions", model);
            return response.IsSuccessStatusCode ? (true, null) : (false, $"Failed ({(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string? error)> SyncTransactionsAsync(Guid customerId)
    {
        try
        {
            var response = await Client.PostAsync(
                $"api/v1/customers/{customerId}/transactions/sync", null);
            return response.IsSuccessStatusCode ? (true, null) : (false, $"Failed ({(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string? error)> CategorizeTransactionAsync(
        Guid transactionId, TransactionCategory category)
    {
        try
        {
            var payload = new { Category = (int)category };
            var response = await Client.PatchAsJsonAsync(
                $"api/v1/transactions/{transactionId}/categorize", payload);
            return response.IsSuccessStatusCode ? (true, null) : (false, $"Failed ({(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, byte[]? data, string? error)> ExportCsvAsync(
        Guid customerId, DateTime? fromDate = null, DateTime? toDate = null,
        TransactionCategory? category = null)
    {
        try
        {
            var qs = new StringBuilder();
            if (fromDate.HasValue) qs.Append($"fromDate={fromDate.Value:yyyy-MM-dd}&");
            if (toDate.HasValue) qs.Append($"toDate={toDate.Value:yyyy-MM-dd}&");
            if (category.HasValue) qs.Append($"category={(int)category.Value}&");

            var response = await Client.GetAsync(
                $"api/v1/customers/{customerId}/transactions/export?{qs}");

            if (!response.IsSuccessStatusCode)
                return (false, null, $"Failed ({(int)response.StatusCode})");

            var bytes = await response.Content.ReadAsByteArrayAsync();
            return (true, bytes, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    private static string BuildFilterQueryString(TransactionFilterParams f)
    {
        var sb = new StringBuilder();
        sb.Append($"pageNumber={f.PageNumber}&pageSize={f.PageSize}&sortDescending={f.SortDescending}");
        if (f.Category.HasValue) sb.Append($"&category={(int)f.Category.Value}");
        if (f.Status.HasValue) sb.Append($"&status={(int)f.Status.Value}");
        if (f.FromDate.HasValue) sb.Append($"&fromDate={f.FromDate.Value:yyyy-MM-ddTHH:mm:ss}");
        if (f.ToDate.HasValue) sb.Append($"&toDate={f.ToDate.Value:yyyy-MM-ddTHH:mm:ss}");
        if (f.MinAmount.HasValue) sb.Append($"&minAmount={f.MinAmount.Value}");
        if (f.MaxAmount.HasValue) sb.Append($"&maxAmount={f.MaxAmount.Value}");
        if (!string.IsNullOrWhiteSpace(f.SearchTerm)) sb.Append($"&searchTerm={Uri.EscapeDataString(f.SearchTerm)}");
        if (!string.IsNullOrWhiteSpace(f.Source)) sb.Append($"&source={Uri.EscapeDataString(f.Source)}");
        if (!string.IsNullOrWhiteSpace(f.SortBy)) sb.Append($"&sortBy={Uri.EscapeDataString(f.SortBy)}");
        return sb.ToString();
    }
}
