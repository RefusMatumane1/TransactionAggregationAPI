using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using StackExchange.Redis;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace TransactionAggregationAPI.RateLimiting;

/// <summary>
/// Named rate-limiter policy ("FixedWindow") applied to all endpoint groups
/// via .RequireRateLimiting("FixedWindow").
///
/// Registered as a singleton so its IConnectionMultiplexer dependency
/// is injected once and reused across requests.
/// The framework resolves this type via HttpContext.RequestServices when
/// processing each request through the rate-limiting middleware.
/// </summary>
internal sealed class RedisFixedWindowPolicy(IConnectionMultiplexer redis, ILoggerFactory loggerFactory) : IRateLimiterPolicy<string>
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<RedisFixedWindowRateLimiter>();

    // Returning null uses the global OnRejected handler configured on RateLimiterOptions.
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        // NOTE: Identity.Name reflects Keycloak's "preferred_username" claim only if a
        // ClaimsMap adds it — using it here would silently fall back to RemoteIpAddress for
        // everyone, collapsing all traffic behind a shared ingress into one bucket. The "sub"
        // claim (the user's own id) is always present on an authenticated request instead.
        // Program.cs sets JwtBearerOptions.MapInboundClaims = false, so read "sub" directly
        // rather than the ClaimTypes.NameIdentifier remap ASP.NET Core no longer applies.
        var key = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        return RateLimitPartition.Get<string>(
            key,
            partitionKey => new RedisFixedWindowRateLimiter(
                redis,
                $"ratelimit:endpoint:{partitionKey}",
                new RedisRateLimiterOptions
                {
                    // 60 req/min per authenticated user or IP per endpoint group.
                    // Allows comfortable interactive use (dashboard loads, filters, etc.)
                    // while still blocking runaway clients.
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    AllowRequestOnRedisFailure = true
                },
                _logger));
    }
}
