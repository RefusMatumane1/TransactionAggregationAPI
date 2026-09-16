using System.Security.Claims;

namespace TransactionAggregationUI.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>The "sub" claim from the Keycloak-issued token — this is the same id the API
    /// uses as CustomerId (see IKeycloakAdminClient on the backend), so it's the current
    /// customer's id.</summary>
    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst("sub")?.Value, out var userId) ? userId : null;
}
