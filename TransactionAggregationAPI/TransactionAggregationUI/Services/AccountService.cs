using System.Net.Http.Json;
using TransactionAggregationUI.Models.Accounts;

namespace TransactionAggregationUI.Services;

public class AccountService
{
    private readonly IHttpClientFactory _factory;

    public AccountService(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Client => _factory.CreateClient("api");

    public async Task<List<AccountModel>> GetAccountsAsync(Guid customerId)
    {
        try
        {
            var result = await Client.GetFromJsonAsync<List<AccountModel>>(
                $"api/v1/customers/{customerId}/accounts");
            return result ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<AccountModel?> GetAccountByIdAsync(Guid customerId, Guid accountId)
    {
        try
        {
            return await Client.GetFromJsonAsync<AccountModel>(
                $"api/v1/customers/{customerId}/accounts/{accountId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool success, string? error)> CreateAccountAsync(Guid customerId, CreateAccountModel model)
    {
        try
        {
            var payload = new
            {
                model.AccountNumber,
                model.AccountName,
                AccountType = (int)model.AccountType,
                model.Currency
            };
            var response = await Client.PostAsJsonAsync($"api/v1/customers/{customerId}/accounts", payload);
            return response.IsSuccessStatusCode ? (true, null) : (false, $"Failed ({(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string? error)> DeactivateAccountAsync(Guid customerId, Guid accountId)
    {
        try
        {
            var response = await Client.PatchAsync(
                $"api/v1/customers/{customerId}/accounts/{accountId}/deactivate", null);
            return response.IsSuccessStatusCode ? (true, null) : (false, $"Failed ({(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
