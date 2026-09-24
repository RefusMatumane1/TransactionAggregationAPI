using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using TransactionAggregationUI.Models.Audit;

namespace TransactionAggregationUI.Services;

public class AuditService
{
    private readonly IHttpClientFactory _factory;

    public AuditService(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Client => _factory.CreateClient("api");

    public async Task<(AuditEventPageModel? page, string? error)> SearchAsync(AuditFilter filter)
    {
        var query = new List<string>
        {
            $"pageNumber={filter.PageNumber}",
            $"pageSize={filter.PageSize}"
        };
        Add(query, "channel", filter.Channel);
        Add(query, "eventType", filter.EventType);
        Add(query, "sourceName", filter.SourceName);
        Add(query, "externalAccountId", filter.ExternalAccountId);
        Add(query, "inboxMessageId", filter.InboxMessageId?.ToString());
        Add(query, "transactionId", filter.TransactionId?.ToString());
        Add(query, "externalTransactionId", filter.ExternalTransactionId);
        Add(query, "from", ToUtcIso(filter.From));
        Add(query, "to", ToUtcIso(filter.To));

        try
        {
            var response = await Client.GetAsync($"api/v1/admin/audit/events?{string.Join('&', query)}");
            if (!response.IsSuccessStatusCode)
                return (null, await DescribeFailureAsync(response));

            return (await response.Content.ReadFromJsonAsync<AuditEventPageModel>(), null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    public async Task<(TransactionLineageModel? lineage, string? error)> GetLineageAsync(Guid transactionId)
    {
        try
        {
            var response = await Client.GetAsync($"api/v1/admin/audit/transactions/{transactionId}/lineage");
            if (response.StatusCode == HttpStatusCode.NotFound)
                return (null, "No ingestion record for this transaction. It may predate the audit trail (e.g. seed data), or the id is wrong.");
            if (!response.IsSuccessStatusCode)
                return (null, await DescribeFailureAsync(response));

            return (await response.Content.ReadFromJsonAsync<TransactionLineageModel>(), null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    private static void Add(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            query.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
    }

    private static string? ToUtcIso(DateTime? local) =>
        local is { } value
            ? DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
            : null;

    private static async Task<string> DescribeFailureAsync(HttpResponseMessage response)
    {
        // ProblemDetails "detail" carries the validation message (e.g. bad page size / date range).
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemModel>();
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
                return problem.Detail;
        }
        catch
        {
            // Not a ProblemDetails body — fall through to the status code.
        }

        return $"Request failed ({(int)response.StatusCode})";
    }

    private sealed class ProblemModel
    {
        public string? Detail { get; set; }
    }
}