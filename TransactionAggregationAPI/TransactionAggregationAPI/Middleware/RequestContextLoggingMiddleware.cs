using Microsoft.Extensions.Primitives;
using Serilog.Context;

namespace TransactionAggregationAPI.Middleware;

public class RequestContextLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestContextLoggingMiddleware> logger)
{
    private const string CorrelationIdHeaderName = "X-Correlation-Id";

    public Task Invoke(HttpContext context)
    {
        var correlationId = GetCorrelationId(context);

        using var serilogProp = LogContext.PushProperty("CorrelationId", correlationId);

        using var scope = logger.BeginScope(
            new Dictionary<string, object> { ["CorrelationId"] = correlationId });

        return next.Invoke(context);
    }

    private static string GetCorrelationId(HttpContext context)
    {
        context.Request.Headers.TryGetValue(
            CorrelationIdHeaderName,
            out StringValues correlationId);

        return correlationId.FirstOrDefault() ?? context.TraceIdentifier;
    }
}
