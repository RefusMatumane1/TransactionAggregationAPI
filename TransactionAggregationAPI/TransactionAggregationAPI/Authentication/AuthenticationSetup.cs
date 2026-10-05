using BuildingBlocks.Application.Abstractions.Authentication;
using BuildingBlocks.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace TransactionAggregationAPI.Authentication
{
    internal static class AuthenticationSetup
    {
        public static WebApplicationBuilder AddKeycloakAuthentication(this WebApplicationBuilder builder)
        {
            var keycloak = builder.Configuration.GetSection("Keycloak");
            var authority = keycloak["Authority"];
            var realm = keycloak["Realm"];
            var publicIssuer = keycloak["PublicIssuer"];
            var audience = keycloak["Audience"];
            if (string.IsNullOrEmpty(authority) || string.IsNullOrEmpty(realm) ||
                string.IsNullOrEmpty(publicIssuer) || string.IsNullOrEmpty(audience))
                throw new InvalidOperationException(
                    "Keycloak:Authority, Keycloak:Realm, Keycloak:PublicIssuer and Keycloak:Audience must all be configured.");

            var requireHttpsMetadata = keycloak.GetValue<bool?>("RequireHttpsMetadata") ?? !builder.Environment.IsDevelopment();
            if (requireHttpsMetadata && !authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "Keycloak:Authority must use https unless Keycloak:RequireHttpsMetadata is explicitly false.");

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MetadataAddress = $"{authority.TrimEnd('/')}/realms/{realm}/.well-known/openid-configuration";

                    options.RequireHttpsMetadata = requireHttpsMetadata;

                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = publicIssuer,
                        ValidAudience = audience,
                        RoleClaimType = "roles"
                    };
                });

            builder.Services.AddAuthorizationBuilder()
                .AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireRole(Roles.Admin))
                .AddPolicy(AuthorizationPolicies.Staff, policy => policy.RequireRole(Roles.Staff, Roles.Admin));

            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<IUserContext, UserContext>();

            return builder;
        }
    }
}