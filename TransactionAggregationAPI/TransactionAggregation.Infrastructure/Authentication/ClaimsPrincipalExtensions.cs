using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace TransactionAggregation.Infrastructure.Authentication
{
    internal static class ClaimsPrincipalExtensions
    {
        // Keycloak-issued tokens carry the user id as the raw "sub" claim. Program.cs sets
        // JwtBearerOptions.MapInboundClaims = false, so ASP.NET Core no longer remaps "sub" to
        // ClaimTypes.NameIdentifier the way it did for the old self-signed tokens — read it
        // directly instead. Keycloak's sub is also the same id used as CustomerId (see
        // IKeycloakAdminClient/CreateCustomerCommandHandler), so this is the customer's id.
        public static Guid GetUserId(this ClaimsPrincipal? principal)
        {
            string? userId = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);

            return Guid.TryParse(userId, out Guid parsedUserId) ?
                parsedUserId :
                throw new ApplicationException("Customer id is unavailable");
        }
    
        private static string? FindFirstValue(this ClaimsPrincipal principal, string claimType)
        {
            ThrowIfNull(principal);
            var claim = principal.FindFirst(claimType);
            return claim?.Value;
        }

        private static void ThrowIfNull(ClaimsPrincipal principal)
        {
            if (principal is null)
                throw new ArgumentException("ClaimsPrincipal is null");
        }
    }
}
