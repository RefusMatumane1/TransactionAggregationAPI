using Microsoft.IdentityModel.JsonWebTokens;
using Serilog;
using Serilog.Events;

namespace TransactionAggregationAPI.Middleware
{
    // Logs the route pattern, never the raw path (caller-supplied values), plus the subject id for attribution.
    internal static class RequestLoggingSetup
    {
        public const string UnmatchedRoute = "(unmatched)";

        private const string MessageTemplate =
            "HTTP {RequestMethod} {RoutePattern} responded {StatusCode} in {Elapsed:0.0000} ms for {UserId}";
        private const string Anonymous = "(anonymous)";

        public static IApplicationBuilder UseRouteTemplateRequestLogging(this IApplicationBuilder app) =>
            app.UseSerilogRequestLogging(options =>
            {
                options.Logger = app.ApplicationServices.GetRequiredService<Serilog.ILogger>();
                options.MessageTemplate = MessageTemplate;
                options.GetMessageTemplateProperties = (httpContext, _, elapsedMs, statusCode) =>
                [
                    new LogEventProperty("RequestMethod", new ScalarValue(httpContext.Request.Method)),
                    new LogEventProperty("RoutePattern", new ScalarValue(RoutePatternOf(httpContext))),
                    new LogEventProperty("StatusCode", new ScalarValue(statusCode)),
                    new LogEventProperty("Elapsed", new ScalarValue(elapsedMs)),
                    new LogEventProperty("UserId", new ScalarValue(
                        httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? Anonymous))
                ];
            });

        public static string RoutePatternOf(HttpContext httpContext) =>
            (httpContext.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? UnmatchedRoute;
    }
}