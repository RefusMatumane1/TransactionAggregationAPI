using System.Net.Http.Headers;
using TransactionAggregationUI.Auth;

namespace TransactionAggregationUI.Services;

public class AuthHeaderHandler : DelegatingHandler
{
    private readonly JwtAuthStateProvider _authProvider;

    public AuthHeaderHandler(JwtAuthStateProvider authProvider)
    {
        _authProvider = authProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _authProvider.GetTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
