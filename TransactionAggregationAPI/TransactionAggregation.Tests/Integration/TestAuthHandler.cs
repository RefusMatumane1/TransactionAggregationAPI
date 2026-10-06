using BuildingBlocks.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace TransactionAggregation.Tests.Integration
{
    public sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string UserIdHeaderName = "X-Test-UserId";
        public const string RolesHeaderName = "X-Test-Roles";
        public const string InstitutionsHeaderName = "X-Test-Institutions";

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

            if (Request.Headers.TryGetValue(InstitutionsHeaderName, out var institutionValues))
            {
                var institutions = institutionValues.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                claims.AddRange(institutions.Select(institution => new Claim(ClaimNames.Institutions, institution)));
            }

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    public static class TestClientExtensions
    {
        public static HttpClient SignedInAs(this HttpClient client, params string[] roles)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, Guid.NewGuid().ToString());
            if (roles.Length > 0)
                client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeaderName, string.Join(',', roles));
            return client;
        }

        public static HttpClient SignedInAsStaffFor(this HttpClient client, params string[] institutions)
        {
            client.SignedInAs("staff");
            if (institutions.Length > 0)
                client.DefaultRequestHeaders.Add(TestAuthHandler.InstitutionsHeaderName, string.Join(',', institutions));
            return client;
        }
    }
}