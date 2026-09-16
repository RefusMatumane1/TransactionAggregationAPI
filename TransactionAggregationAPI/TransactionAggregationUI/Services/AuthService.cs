using System.Net.Http.Json;
using TransactionAggregationUI.Models.Auth;

namespace TransactionAggregationUI.Services;

/// <summary>
/// Registration only — login/logout/session state are handled by the OIDC library
/// (Microsoft.AspNetCore.Components.WebAssembly.Authentication, wired up in Program.cs) talking
/// to Keycloak directly, not by this app.
/// </summary>
public class AuthService
{
    private readonly IHttpClientFactory _factory;

    public AuthService(IHttpClientFactory factory)
    {
        _factory = factory;
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
}
