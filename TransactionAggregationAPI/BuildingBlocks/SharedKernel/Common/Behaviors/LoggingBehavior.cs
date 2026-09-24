using MediatR;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using System.Diagnostics;

namespace SharedKernel.Common.Behaviors
{
    public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> _logger) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var requestId = Guid.NewGuid().ToString();

            // Deliberately never the request body: requests carry customer PII (email, name)
            // and whole transaction batches, and anything pushed here is stamped on every log
            // event in the scope. CorrelationId is owned by RequestContextLoggingMiddleware
            // (HTTP) and the trace (workers) — overwriting it here would split one request's
            // logs across two ids.
            using (LogContext.PushProperty("RequestId", requestId))
            using (LogContext.PushProperty("RequestName", requestName))
            {
                _logger.LogInformation("Processing request {RequestName} {RequestId}", requestName, requestId);

                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    var response = await next();
                    stopwatch.Stop();

                    _logger.LogInformation(
                        "Completed request {RequestName} {RequestId} in {ElapsedMs}ms",
                        requestName, requestId, stopwatch.ElapsedMilliseconds);

                    return response;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed request {RequestName} {RequestId}", requestName, requestId);
                    throw;
                }
            }
        }
    }
}