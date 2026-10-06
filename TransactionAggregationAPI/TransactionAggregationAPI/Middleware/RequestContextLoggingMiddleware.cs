using BuildingBlocks.Messaging.Observability;
using BuildingBlocks.Web;
using Serilog.Context;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TransactionAggregationAPI.Middleware
{
    public partial class RequestContextLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestContextLoggingMiddleware> logger)
    {
        public const string CorrelationIdHeaderName = CorrelationContext.HeaderName;

        [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
        private static partial Regex SafeCorrelationId();

        public async Task Invoke(HttpContext context)
        {
            var correlationId = GetCorrelationId(context);
            CorrelationContext.Set(context, correlationId);
            Activity.Current?.SetBaggage(MessagingTelemetry.CorrelationBaggageKey, correlationId);

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
}