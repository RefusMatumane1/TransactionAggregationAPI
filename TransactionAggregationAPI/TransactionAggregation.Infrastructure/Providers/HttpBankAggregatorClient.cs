using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Infrastructure.Providers
{
    /// <summary>
    /// OAuth2 (RFC 6749) client for the configured account-data aggregator. The
    /// authorization-code/refresh-token exchange below is spec-standard; the account call is
    /// written against a generic REST shape and is the part you must adapt to your chosen
    /// aggregator's actual API (Stitch's public API, for example, is GraphQL — swap the
    /// GetLinkedAccountAsync body for a POST with a GraphQL query if so). Transaction data
    /// itself is never pulled through this client — the aggregator pushes it to
    /// WebhookEndpoints instead. Resilience (retry/circuit-breaker/timeout) comes from the
    /// standard handler registered app-wide in ServiceDefaults — this class doesn't need its
    /// own Polly policy.
    /// </summary>
    internal sealed class HttpBankAggregatorClient : IBankAggregatorClient
    {
        private readonly HttpClient _httpClient;
        private readonly BankAggregatorOptions _options;
        private readonly ILogger<HttpBankAggregatorClient> _logger;

        public HttpBankAggregatorClient(
            HttpClient httpClient,
            IOptions<BankAggregatorOptions> options,
            ILogger<HttpBankAggregatorClient> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;
        }

        public string BuildAuthorizationUrl(Institution institution, string state)
        {
            var query = new Dictionary<string, string?>
            {
                ["client_id"] = _options.ClientId,
                ["redirect_uri"] = _options.RedirectUri,
                ["response_type"] = "code",
                ["scope"] = "accounts:read transactions:read",
                ["state"] = state,
                // Provider-specific institution hint — confirm the actual query param name
                // your aggregator expects (Stitch calls this concept a "bank" in its consent UI).
                ["institution"] = institution.ToString()
            };

            return BuildUrlWithQuery(_options.AuthorizeEndpoint, query);
        }

        private static string BuildUrlWithQuery(string baseUrl, Dictionary<string, string?> query)
        {
            var pairs = query
                .Where(kv => kv.Value is not null)
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}");

            return $"{baseUrl}{(baseUrl.Contains('?') ? '&' : '?')}{string.Join('&', pairs)}";
        }

        public async Task<AggregatorTokenResult> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = _options.RedirectUri,
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret
            };

            return await PostTokenRequestAsync(form, cancellationToken);
        }

        public async Task<AggregatorTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret
            };

            return await PostTokenRequestAsync(form, cancellationToken);
        }

        private async Task<AggregatorTokenResult> PostTokenRequestAsync(
            Dictionary<string, string> form, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(form)
            };

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            await ThrowIfUnauthorizedAsync(response, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Aggregator token endpoint returned an empty body");

            return new AggregatorTokenResult(
                payload.AccessToken,
                payload.RefreshToken,
                DateTime.UtcNow.AddSeconds(payload.ExpiresInSeconds));
        }

        public async Task<AggregatorLinkedAccountResult> GetLinkedAccountAsync(string accessToken, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _options.AccountEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            await ThrowIfUnauthorizedAsync(response, cancellationToken);
            response.EnsureSuccessStatusCode();

            var account = await response.Content.ReadFromJsonAsync<AggregatorAccountResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Aggregator account endpoint returned an empty body");

            return new AggregatorLinkedAccountResult(
                account.Id,
                account.AccountNumber,
                account.AccountName,
                account.AccountType,
                account.Currency);
        }

        private async Task ThrowIfUnauthorizedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.StatusCode != HttpStatusCode.Unauthorized)
                return;

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Aggregator rejected the request as unauthorized (token expired/revoked)");
            throw new BankAggregatorUnauthorizedException($"Aggregator returned 401: {body}");
        }

        // ── Provider response DTOs ──────────────────────────────────────────────────
        // OAuthTokenResponse follows RFC 6749 §5.1 and should hold for any spec-compliant
        // provider. The account/transaction shapes below are illustrative — replace their
        // field names to match your aggregator's actual response schema.

        private sealed record OAuthTokenResponse(
            [property: JsonPropertyName("access_token")] string AccessToken,
            [property: JsonPropertyName("refresh_token")] string RefreshToken,
            [property: JsonPropertyName("expires_in")] int ExpiresInSeconds);

        private sealed record AggregatorAccountResponse(
            [property: JsonPropertyName("id")] string Id,
            [property: JsonPropertyName("accountNumber")] string AccountNumber,
            [property: JsonPropertyName("accountName")] string AccountName,
            [property: JsonPropertyName("accountType")] string AccountType,
            [property: JsonPropertyName("currency")] string Currency);
    }
}
