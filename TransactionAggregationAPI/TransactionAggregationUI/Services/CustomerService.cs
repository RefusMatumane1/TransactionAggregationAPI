using System.Net.Http.Json;
using TransactionAggregationUI.Models.Customers;

namespace TransactionAggregationUI.Services;

public class CustomerService
{
    private readonly IHttpClientFactory _factory;

    public CustomerService(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    private HttpClient Client => _factory.CreateClient("api");

    public async Task<CustomerModel?> GetCustomerAsync(Guid customerId)
    {
        try
        {
            return await Client.GetFromJsonAsync<CustomerModel>($"api/v1/customers/{customerId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool success, string? error)> UpdateCustomerAsync(Guid customerId, string email, string name)
    {
        try
        {
            var response = await Client.PutAsJsonAsync($"api/v1/customers/{customerId}", new { Email = email, Name = name });
            return response.IsSuccessStatusCode ? (true, null) : (false, $"Update failed ({(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string? error)> DeleteCustomerAsync(Guid customerId)
    {
        try
        {
            var response = await Client.DeleteAsync($"api/v1/customers/{customerId}");
            return response.IsSuccessStatusCode ? (true, null) : (false, $"Delete failed ({(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
