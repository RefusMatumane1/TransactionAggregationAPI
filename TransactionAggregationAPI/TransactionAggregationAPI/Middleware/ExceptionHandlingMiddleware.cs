using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace TransactionAggregationAPI.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public ExceptionHandlingMiddleware(
            RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred");
                await HandleExceptionAsync(context, ex, _environment);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception, IWebHostEnvironment environment)
        {

            var problemDetails = new ProblemDetails
            {
                Title = "An error occurred while processing your request",
                Detail = environment.IsDevelopment() ? exception.Message : "An unexpected error occurred.",
                Status = StatusCodes.Status500InternalServerError,
                Type = "https://httpstatuses.com/500"
            };

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
        }
    }
}