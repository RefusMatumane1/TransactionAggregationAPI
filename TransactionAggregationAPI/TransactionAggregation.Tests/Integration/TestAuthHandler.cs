using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace TransactionAggregation.Tests.Integration;

/// <summary>
/// Stands in for the real Keycloak-issued JwtBearer scheme in integration tests, so they need
/// no live Keycloak. Authenticates a request as whichever customer id is passed in the
/// X-Test-UserId header (mirroring the "sub" claim a real Keycloak token would carry) — a
/// request without that header is treated as anonymous, exactly like a request with no bearer
/// token. An optional, comma-separated X-Test-Roles header adds role claims (e.g. "admin") for
/// tests exercising RequireAuthorization("Admin") — uses ClaimTypes.Role, the default
/// RoleClaimType for a ClaimsIdentity built without an explicit one, independent of the
/// "roles"-named claim the real JwtBearer scheme is configured for (see Program.cs) since this
/// is a wholly separate, test-only authentication scheme.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserIdHeaderName = "X-Test-UserId";
    public const string RolesHeaderName = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeaderName, out var values) ||
            !Guid.TryParse(values.ToString(), out var userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, userId.ToString()) };

        if (Request.Headers.TryGetValue(RolesHeaderName, out var roleValues))
        {
            var roles = roleValues.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
