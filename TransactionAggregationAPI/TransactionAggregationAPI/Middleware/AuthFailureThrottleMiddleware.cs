using TransactionAggregationAPI.RateLimiting;

namespace TransactionAggregationAPI.Middleware
{
    internal sealed class AuthFailureThrottleMiddleware(RequestDelegate next, IAuthFailureThrottle throttle)
    {
        public async Task Invoke(HttpContext context)
        {
            var client = context.Connection.RemoteIpAddress?.ToString();
            if (client is null)
            {
                await next(context);
                return;
            }

            if (await throttle.BlockedForAsync(client, context.RequestAborted) is { } retryAfter)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.RetryAfter =
                    Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                return;
            }

            await next(context);

            if (context.Response.StatusCode == StatusCodes.Status401Unauthorized)
                await throttle.RecordFailureAsync(client, context.RequestAborted);
        }
    }
}