using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace TransactionAggregationUI.Auth;

public class JwtAuthStateProvider : AuthenticationStateProvider
{
    private const string StorageKey = "jwt";
    private readonly IJSRuntime _js;
    private string? _cachedToken;

    public JwtAuthStateProvider(IJSRuntime js)
    {
        _js = js;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await GetTokenAsync();

        if (string.IsNullOrWhiteSpace(token))
            return Unauthenticated();

        var claims = ParseClaimsFromJwt(token);
        if (claims == null)
            return Unauthenticated();

        // Check expiry claim
        var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
        if (expClaim != null && long.TryParse(expClaim.Value, out var exp))
        {
            var expiry = DateTimeOffset.FromUnixTimeSeconds(exp);
            if (expiry < DateTimeOffset.UtcNow)
            {
                await ClearTokenAsync();
                return Unauthenticated();
            }
        }

        var identity = new ClaimsIdentity(claims, "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public async Task<string?> GetTokenAsync()
    {
        _cachedToken ??= await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        return _cachedToken;
    }

    public async Task SetTokenAsync(string token)
    {
        _cachedToken = token;
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, token);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task ClearTokenAsync()
    {
        _cachedToken = null;
        await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
        NotifyAuthenticationStateChanged(Task.FromResult(Unauthenticated()));
    }

    public string? GetUserId()
    {
        if (string.IsNullOrWhiteSpace(_cachedToken)) return null;
        var claims = ParseClaimsFromJwt(_cachedToken);
        return claims?.FirstOrDefault(c => c.Type == "sub")?.Value
            ?? claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
    }

    private static AuthenticationState Unauthenticated() =>
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private static IEnumerable<Claim>? ParseClaimsFromJwt(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3) return null;

            var payload = parts[1];
            // Pad base64url to standard base64
            var padded = payload.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }

            var bytes = Convert.FromBase64String(padded);
            var json = Encoding.UTF8.GetString(bytes);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (dict == null) return null;

            var claims = new List<Claim>();
            foreach (var (key, value) in dict)
            {
                if (value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in value.EnumerateArray())
                        claims.Add(new Claim(key, element.ToString()));
                }
                else
                {
                    claims.Add(new Claim(key, value.ToString()));
                }
            }
            return claims;
        }
        catch
        {
            return null;
        }
    }
}
