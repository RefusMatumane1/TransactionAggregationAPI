using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;
using System.Security.Claims;
using System.Text.Json;

namespace TransactionAggregationUI.Auth
{
    // Keycloak sends realm roles as a JSON array ("roles": ["staff"]). The default factory turns that
    // into one claim whose value is the array's text, so IsInRole("staff") fails; this splits it into
    // one role claim per entry.
    public sealed class RolesClaimsPrincipalFactory(IAccessTokenProviderAccessor accessor)
        : AccountClaimsPrincipalFactory<RemoteUserAccount>(accessor)
    {
        public const string RoleClaim = "roles";

        public override async ValueTask<ClaimsPrincipal> CreateUserAsync(
            RemoteUserAccount account, RemoteAuthenticationUserOptions options)
        {
            var user = await base.CreateUserAsync(account, options);
            if (user.Identity is not ClaimsIdentity { IsAuthenticated: true } identity)
                return user;

            foreach (var claim in identity.FindAll(RoleClaim).ToList())
            {
                if (!claim.Value.TrimStart().StartsWith('['))
                    continue;

                identity.RemoveClaim(claim);
                foreach (var role in JsonSerializer.Deserialize<string[]>(claim.Value) ?? [])
                    identity.AddClaim(new Claim(RoleClaim, role));
            }

            return user;
        }
    }
}