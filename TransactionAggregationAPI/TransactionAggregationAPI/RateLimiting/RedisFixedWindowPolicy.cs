using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using StackExchange.Redis;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace TransactionAggregationAPI.RateLimiting;

internal sealed class RedisFixedWindowPolicy(IConnectionMultiplexer redis, ILoggerFactory loggerFactory) : IRateLimiterPolicy<string>
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<RedisFixedWindowRateLimiter>();

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {

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

                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    AllowRequestOnRedisFailure = true
                },
                _logger));
    }
}