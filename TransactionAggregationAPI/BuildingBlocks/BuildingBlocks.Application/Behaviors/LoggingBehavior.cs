using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BuildingBlocks.Application.Behaviors
{
    public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private static readonly TimeSpan SlowRequestThreshold = TimeSpan.FromMilliseconds(500);

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var requestId = Guid.NewGuid().ToString();

            using var scope = logger.BeginScope(new Dictionary<string, object>
            {
                ["RequestId"] = requestId,
                ["RequestName"] = requestName
            });

            logger.LogInformation("Processing request {RequestName} {RequestId}", requestName, requestId);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var response = await next();
                stopwatch.Stop();

                logger.Log(
                    stopwatch.Elapsed > SlowRequestThreshold ? LogLevel.Warning : LogLevel.Information,
                    "Completed request {RequestName} {RequestId} in {ElapsedMs}ms",
                    requestName, requestId, stopwatch.ElapsedMilliseconds);

                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed request {RequestName} {RequestId} after {ElapsedMs}ms",
                    requestName, requestId, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}