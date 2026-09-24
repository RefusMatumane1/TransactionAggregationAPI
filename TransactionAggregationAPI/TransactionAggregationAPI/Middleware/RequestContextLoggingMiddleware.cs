using Serilog.Context;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TransactionAggregationAPI.Middleware;

/// <summary>
/// Gives every request one correlation id: the caller's X-Correlation-Id when it's a safe,
/// bounded token, otherwise the W3C trace id — so logs, traces and audit records line up.
/// The id is echoed on the response, so a caller (or support) can quote it back.
/// </summary>
public partial class RequestContextLoggingMiddleware(
    RequestDelegate next,
    ILogger<RequestContextLoggingMiddleware> logger)
{
    public const string CorrelationIdHeaderName = "X-Correlation-Id";

    /// <summary>
    /// Client-supplied ids end up in every log line of the request, so only short
    /// [A-Za-z0-9._-] tokens are accepted — no CR/LF log forging, no unbounded values.
    /// </summary>
    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex SafeCorrelationId();

    public async Task Invoke(HttpContext context)
    {
        var correlationId = GetCorrelationId(context);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using var serilogProp = LogContext.PushProperty("CorrelationId", correlationId);
        using var scope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });

        await next(context);
    }

    private static string GetCorrelationId(HttpContext context)
    {
        var supplied = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault();

        return supplied is not null && SafeCorrelationId().IsMatch(supplied)
            ? supplied
            : Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    }
}