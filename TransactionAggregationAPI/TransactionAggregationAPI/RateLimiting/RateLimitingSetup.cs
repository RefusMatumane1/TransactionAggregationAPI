using BuildingBlocks.Web;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using StackExchange.Redis;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace TransactionAggregationAPI.RateLimiting
{
    internal static class RateLimitingSetup
    {
        private static readonly RedisRateLimiterOptions GlobalLimit = new() { PermitLimit = 100, Window = TimeSpan.FromMinutes(1) };
        private static readonly RedisRateLimiterOptions ApiGroupLimit = new() { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) };

        public static IServiceCollection AddRedisRateLimiting(this IServiceCollection services)
        {
            services.AddSingleton<ApiGroupRateLimitPolicy>();
            services.AddRateLimiter(options =>
            {
                options.AddPolicy<string, ApiGroupRateLimitPolicy>(RateLimitPolicies.FixedWindow);
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = (context, _) =>
                {
                    WriteRetryAfter(context.HttpContext, context.Lease);
                    return ValueTask.CompletedTask;
                };
            });

            services.AddOptions<RateLimiterOptions>()
                .Configure<IConnectionMultiplexer, ILoggerFactory>((options, redis, loggerFactory) =>
                {
                    var logger = loggerFactory.CreateLogger<RedisFixedWindowRateLimiter>();
                    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                        Partition(redis, "global", PartitionKey(context), GlobalLimit, logger));
                });

            return services;
        }

        public static void WriteRetryAfter(HttpContext httpContext, RateLimitLease lease)
        {
            if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                httpContext.Response.Headers.RetryAfter =
                    Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string PartitionKey(HttpContext context) =>
            context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        private static RateLimitPartition<string> Partition(
            IConnectionMultiplexer redis, string scope, string key, RedisRateLimiterOptions limit, ILogger logger) =>
            RateLimitPartition.Get(key, partitionKey =>
                new RedisFixedWindowRateLimiter(redis, scope, partitionKey, limit, logger));

        private sealed class ApiGroupRateLimitPolicy(IConnectionMultiplexer redis, ILoggerFactory loggerFactory)
            : IRateLimiterPolicy<string>
        {
            private readonly ILogger _logger = loggerFactory.CreateLogger<RedisFixedWindowRateLimiter>();

            public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

            public RateLimitPartition<string> GetPartition(HttpContext httpContext) =>
                Partition(redis, "endpoint", PartitionKey(httpContext), ApiGroupLimit, _logger);
        }
    }
}