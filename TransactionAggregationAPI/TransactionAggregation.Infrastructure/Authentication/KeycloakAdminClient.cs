using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Abstractions.Authentication;
using TransactionAggregation.Application.Abstractions.Authentication;

namespace TransactionAggregation.Infrastructure.Authentication
{
    internal sealed class KeycloakAdminClient(
    HttpClient httpClient,
    IOptions<KeycloakOptions> options,
    ILogger<KeycloakAdminClient> logger) : IKeycloakAdminClient
    {
        private readonly KeycloakOptions _options = options.Value;

        public async Task<Guid> CreateUserAsync(string email, string name, string password, CancellationToken cancellationToken = default)
        {
            var accessToken = await GetAdminAccessTokenAsync(cancellationToken);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"admin/realms/{_options.Realm}/users")
            {
                Content = JsonContent.Create(new CreateUserRequest(
                    Username: email,
                    Email: email,
                    FirstName: name,
                    Enabled: true,
                    EmailVerified: true,
                    Credentials: [new CredentialRequest("password", password, false)]))
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new Application.Abstractions.Authentication.KeycloakUserConflictException(
                    $"A Keycloak user with email '{email}' already exists.");

            response.EnsureSuccessStatusCode();

            var location = response.Headers.Location
                            ?? throw new InvalidOperationException("Keycloak did not return a Location header for the created user.");

            var userId = location.Segments[^1].TrimEnd('/');
            return Guid.Parse(userId);
        }

        public async Task<Guid?> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            var accessToken = await GetAdminAccessTokenAsync(cancellationToken);

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"admin/realms/{_options.Realm}/users?email={Uri.EscapeDataString(email)}&exact=true");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var users = await response.Content.ReadFromJsonAsync<List<KeycloakUserResponse>>(cancellationToken) ?? [];
            return users.Count > 0 ? Guid.Parse(users[0].Id) : null;
        }

        private async Task<string> GetAdminAccessTokenAsync(CancellationToken cancellationToken)
        {
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.AdminClientId,
                ["client_secret"] = _options.AdminClientSecret
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"realms/{_options.Realm}/protocol/openid-connect/token")
            {
                Content = new FormUrlEncodedContent(form)
            };

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Keycloak admin token request failed with {StatusCode}", response.StatusCode);
            }
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Keycloak token endpoint returned an empty body");

            return payload.AccessToken;
        }

        private sealed record CreateUserRequest(
            [property: JsonPropertyName("username")] string Username,
            [property: JsonPropertyName("email")] string Email,
            [property: JsonPropertyName("firstName")] string FirstName,
            [property: JsonPropertyName("enabled")] bool Enabled,
            [property: JsonPropertyName("emailVerified")] bool EmailVerified,
            [property: JsonPropertyName("credentials")] List<CredentialRequest> Credentials);

        private sealed record CredentialRequest(
            [property: JsonPropertyName("type")] string Type,
            [property: JsonPropertyName("value")] string Value,
            [property: JsonPropertyName("temporary")] bool Temporary);

        private sealed record TokenResponse(
            [property: JsonPropertyName("access_token")] string AccessToken);

        private sealed record KeycloakUserResponse(
            [property: JsonPropertyName("id")] string Id);
    }
}