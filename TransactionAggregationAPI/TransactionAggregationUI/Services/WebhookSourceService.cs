using System.Net.Http.Json;
using TransactionAggregationUI.Models.WebhookSources;

namespace TransactionAggregationUI.Services;

/// <summary>Admin-only — every call here hits an endpoint gated behind RequireAuthorization
/// ("Admin") on the API, so this only works for a signed-in user with the "admin" realm role.</summary>
public class WebhookSourceService
{
    private readonly IHttpClientFactory _factory;

    public WebhookSourceService(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Client => _factory.CreateClient("api");

    public async Task<List<WebhookSourceModel>> GetSourcesAsync()
    {
        try
        {
            var result = await Client.GetFromJsonAsync<List<WebhookSourceModel>>("api/v1/admin/webhook-sources");
            return result ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<(bool success, CreateWebhookSourceResultModel? result, string? error)> CreateSourceAsync(string name)
    {
        try
        {
            var response = await Client.PostAsJsonAsync("api/v1/admin/webhook-sources", new { Name = name });
            if (!response.IsSuccessStatusCode)
                return (false, null, $"Failed ({(int)response.StatusCode})");

            var result = await response.Content.ReadFromJsonAsync<CreateWebhookSourceResultModel>();
            return (true, result, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool success, string? apiKey, string? error)> RotateKeyAsync(Guid id)
    {
        try
        {
            var response = await Client.PostAsync($"api/v1/admin/webhook-sources/{id}/rotate", null);
            if (!response.IsSuccessStatusCode)
                return (false, null, $"Failed ({(int)response.StatusCode})");

            var result = await response.Content.ReadFromJsonAsync<RotateWebhookSourceKeyResultModel>();
            return (true, result?.ApiKey, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool success, string? error)> ActivateAsync(Guid id)
    {
        try
        {
            var response = await Client.PostAsync($"api/v1/admin/webhook-sources/{id}/activate", null);
            return response.IsSuccessStatusCode ? (true, null) : (false, $"Failed ({(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string? error)> DeactivateAsync(Guid id)
    {
        try
        {
            var response = await Client.PostAsync($"api/v1/admin/webhook-sources/{id}/deactivate", null);
            return response.IsSuccessStatusCode ? (true, null) : (false, $"Failed ({(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
