

using System.Security.Claims;

namespace TransactionAggregation.Infrastructure.Authentication
{
    internal static class ClaimsPrincipalExtensions
    {
        public static Guid GetUserId(this ClaimsPrincipal? principal)
        {
            string? userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

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
