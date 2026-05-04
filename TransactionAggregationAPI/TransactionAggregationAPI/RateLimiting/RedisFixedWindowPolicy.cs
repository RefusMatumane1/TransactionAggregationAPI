using Microsoft.AspNetCore.RateLimiting;
using StackExchange.Redis;
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
internal sealed class RedisFixedWindowPolicy(IConnectionMultiplexer redis) : IRateLimiterPolicy<string>
{
    // Returning null uses the global OnRejected handler configured on RateLimiterOptions.
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var key = httpContext.User.Identity?.Name
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
                }));
    }
}
