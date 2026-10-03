using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace Microsoft.Extensions.Hosting
{
    public static class Extensions
    {
        public const string LivenessEndpointPath = "/liveness";

        public const string ReadinessEndpointPath = "/readiness";

        public const string HealthEndpointPath = "/health";

        public const string LiveTag = "live";
        public const string ReadyTag = "ready";

        public const string MessagingActivitySourceName = "TransactionAggregation.Messaging";

        public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
        {
            builder.ConfigureOpenTelemetry();

            builder.AddDefaultHealthChecks();

            builder.Services.AddServiceDiscovery();

            builder.Services.ConfigureResilientHttpClients();

            return builder;
        }

        public static IServiceCollection ConfigureResilientHttpClients(this IServiceCollection services)
        {
            services.ConfigureHttpClientDefaults(http =>
            {
                http.AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());

                http.AddServiceDiscovery();
            });

            return services;
        }

        public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
        {
            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
            });

            builder.Services.AddOpenTelemetry()
                .WithMetrics(metrics =>
                {
                    metrics.AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddRuntimeInstrumentation();
                })
                .WithTracing(tracing =>
                {
                    tracing.AddSource(builder.Environment.ApplicationName)
                        .AddSource(MessagingActivitySourceName)
                        .AddAspNetCoreInstrumentation(options =>
                            options.Filter = context =>
                                !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                                && !context.Request.Path.StartsWithSegments(LivenessEndpointPath)
                                && !context.Request.Path.StartsWithSegments(ReadinessEndpointPath))
                        .AddHttpClientInstrumentation(options =>
                            options.EnrichWithHttpRequestMessage = OutboundUrlRedaction.Enrich)
                        .AddEntityFrameworkCoreInstrumentation()
                        .AddRedisInstrumentation();
                });

            builder.AddOpenTelemetryExporters();

            builder.AddSeqEndpoint("seq", settings => settings.DisableHealthChecks = true);

            return builder;
        }

        private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
        {
            var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

            if (useOtlpExporter)
            {
                builder.Services.AddOpenTelemetry().UseOtlpExporter();
            }

            return builder;
        }

        public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
        {
            builder.Services.AddHealthChecks()
                .AddCheck("self", () => HealthCheckResult.Healthy(), [LiveTag])
                .AddRedis(
                    connectionMultiplexerFactory: sp => sp.GetRequiredService<IConnectionMultiplexer>(),
                    name: "redis",

                    failureStatus: HealthStatus.Degraded,
                    tags: [ReadyTag]);

            return builder;
        }

        public static WebApplication MapDefaultEndpoints(this WebApplication app)
        {
            app.MapHealthChecks(LivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains(LiveTag),
                ResponseWriter = WriteStatusOnlyResponse
            });

            app.MapHealthChecks(ReadinessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains(ReadyTag),
                ResponseWriter = WriteStatusOnlyResponse
            });

            app.MapHealthChecks(HealthEndpointPath, new HealthCheckOptions
            {
                ResponseWriter = app.Environment.IsDevelopment()
                    ? WriteDetailedResponse
                    : WriteStatusOnlyResponse
            });

            return app;
        }

        private static Task WriteStatusOnlyResponse(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "text/plain; charset=utf-8";
            return context.Response.WriteAsync(report.Status.ToString());
        }

        private static Task WriteDetailedResponse(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json; charset=utf-8";

            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                totalDuration = report.TotalDuration,
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    duration = e.Value.Duration,
                    description = e.Value.Description,
                    exception = e.Value.Exception?.Message
                })
            });

            return context.Response.WriteAsync(result);
        }
    }
}