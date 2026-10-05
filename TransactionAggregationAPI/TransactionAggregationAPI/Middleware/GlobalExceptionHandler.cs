using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TransactionAggregationAPI.Middleware
{
    internal sealed class GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var problem = exception is BadHttpRequestException badRequest
                ? new ProblemDetails
                {
                    Status = badRequest.StatusCode,
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                    Title = "The request could not be read",
                    Detail = "The request body or parameters are malformed."
                }
                : new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                    Title = "An error occurred while processing your request",
                    Detail = "An unexpected error occurred."
                };

            if (problem.Status >= StatusCodes.Status500InternalServerError)
                logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            else
                logger.LogWarning("Rejected malformed request {Method} {Path}: {Reason}",
                    httpContext.Request.Method, httpContext.Request.Path, exception.Message);

            httpContext.Response.StatusCode = problem.Status!.Value;

            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = problem
            });
        }
    }
}