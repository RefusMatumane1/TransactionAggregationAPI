using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Customers.Application.Ports;
using SharedKernel.Abstractions.Authentication;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Modules.Customers.Infrastructure.Authentication
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
            var (firstName, lastName) = SplitName(name);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"admin/realms/{_options.Realm}/users")
            {
                Content = JsonContent.Create(new CreateUserRequest(
                    Username: email,
                    Email: email,
                    FirstName: firstName,
                    LastName: lastName,
                    Enabled: true,
                    EmailVerified: true,
                    Credentials: [new CredentialRequest("password", password, false)]))
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Conflict)
                throw new KeycloakUserConflictException(
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

        /// <summary>
        /// The realm's user profile requires both names; a user missing either is "not fully set
        /// up" — refused a password login and forced through a profile form on first browser
        /// login. The first word is the first name and the rest the surname ("Pieter van der
        /// Merwe" → "Pieter" / "van der Merwe"); a single-word name is used for both.
        /// </summary>
        internal static (string FirstName, string LastName) SplitName(string name)
        {
            var parts = name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length == 2 ? (parts[0], parts[1]) : (parts[0], parts[0]);
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
            [property: JsonPropertyName("lastName")] string LastName,
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