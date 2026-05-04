using System.Net.Http.Json;
using TransactionAggregationUI.Auth;
using TransactionAggregationUI.Models.Auth;

namespace TransactionAggregationUI.Services;

public class AuthService
{
    private readonly IHttpClientFactory _factory;
    private readonly JwtAuthStateProvider _authProvider;

    public AuthService(IHttpClientFactory factory, JwtAuthStateProvider authProvider)
    {
        _factory = factory;
        _authProvider = authProvider;
    }

    public async Task<(bool success, string? error)> LoginAsync(LoginRequest request)
    {
        try
        {
            var client = _factory.CreateClient("api");
            var response = await client.PostAsJsonAsync("api/v1/customers/login", request);
            if (!response.IsSuccessStatusCode)
                return (false, $"Login failed ({(int)response.StatusCode})");

            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (result?.Token is null)
                return (false, "Invalid response from server");

            await _authProvider.SetTokenAsync(result.Token);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string? error)> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var client = _factory.CreateClient("api");
            var payload = new { Email = request.Email, Name = request.Name, password = request.Password };
            var response = await client.PostAsJsonAsync("api/v1/customers", payload);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                return (false, string.IsNullOrWhiteSpace(body) ? $"Registration failed ({(int)response.StatusCode})" : body);
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task LogoutAsync()
    {
        await _authProvider.ClearTokenAsync();
    }

    public string? GetCurrentUserId() => _authProvider.GetUserId();
}
