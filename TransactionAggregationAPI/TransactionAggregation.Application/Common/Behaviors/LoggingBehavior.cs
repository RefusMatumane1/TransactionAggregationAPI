using MediatR;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using System.Diagnostics;

namespace TransactionAggregation.Application.Common.Behaviors
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
            var correlationId = Activity.Current?.Id ?? requestId;

            using (LogContext.PushProperty("RequestId", requestId))
            using (LogContext.PushProperty("CorrelationId", correlationId))
            using (LogContext.PushProperty("RequestName", requestName))
            using (LogContext.PushProperty("Request", request, true))
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
