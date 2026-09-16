using System.Security.Claims;

namespace TransactionAggregationUI.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
    Guid.TryParse(principal.FindFirst("sub")?.Value, out var userId) ? userId : null;
}