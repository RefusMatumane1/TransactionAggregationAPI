using System.Net.Http.Json;
using System.Text.Json;
using TransactionAggregationUI.Models.BankLinks;

namespace TransactionAggregationUI.Services;

public class BankLinkService
{
    private readonly IHttpClientFactory _factory;

    public BankLinkService(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Client => _factory.CreateClient("api");

    // The callback endpoint is anonymous; the authorized client would throw if the login
    // session didn't survive the round trip to the bank's consent page.
    private HttpClient AnonymousClient => _factory.CreateClient("api-anonymous");

    public async Task<List<BankLinkModel>> GetLinksAsync(Guid customerId)
    {
        try
        {
            var result = await Client.GetFromJsonAsync<List<BankLinkModel>>($"api/v1/customers/{customerId}/bank-links");
            return result ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Starts the consent flow; on success the caller sends the browser to the returned URL.</summary>
    public async Task<(string? authorizationUrl, string? error)> InitiateAsync(Guid customerId, Institution institution)
    {
        try
        {
            var response = await Client.PostAsJsonAsync(
                $"api/v1/customers/{customerId}/bank-links", new { Institution = (int)institution });
            if (!response.IsSuccessStatusCode)
                return (null, await ProblemDetailAsync(response));

            var result = await response.Content.ReadFromJsonAsync<InitiateBankLinkResultModel>();
            return (result?.AuthorizationUrl, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    /// <summary>Completes the link with what the bank's consent page sent back.</summary>
    public async Task<(Guid? accountId, string? error)> CompleteAsync(string code, string state)
    {
        try
        {
            var response = await AnonymousClient.GetAsync(
                $"api/v1/bank-links/callback?code={Uri.EscapeDataString(code)}&state={Uri.EscapeDataString(state)}");
            if (!response.IsSuccessStatusCode)
                return (null, await ProblemDetailAsync(response));

            var result = await response.Content.ReadFromJsonAsync<CompleteBankLinkResultModel>();
            return (result?.AccountId, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    /// <summary>The API's ProblemDetails "detail" is written for people; fall back to the status code.</summary>
    private static async Task<string> ProblemDetailAsync(HttpResponseMessage response)
    {
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (json.RootElement.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } text)
                return text;
        }
        catch (JsonException)
        {
        }

        return $"Failed ({(int)response.StatusCode})";
    }
}