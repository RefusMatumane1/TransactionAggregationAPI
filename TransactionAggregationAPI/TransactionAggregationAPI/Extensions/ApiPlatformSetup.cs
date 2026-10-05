using Asp.Versioning;
using Microsoft.AspNetCore.HttpOverrides;
using System.Diagnostics;
using TransactionAggregationAPI.Middleware;

namespace TransactionAggregationAPI.Extensions
{
    internal static class ApiPlatformSetup
    {
        public const string CorsPolicyName = "Default";

        public const long MaxRequestBodyBytes = 1024 * 1024;

        public static WebApplicationBuilder AddApiPlatform(this WebApplicationBuilder builder)
        {
            builder.Services
                .AddApiVersioning(options =>
                {
                    options.AssumeDefaultVersionWhenUnspecified = true;
                    options.DefaultApiVersion = new ApiVersion(1, 0);
                    options.ReportApiVersions = true;
                })
                .AddApiExplorer(options =>
                {
                    options.GroupNameFormat = "'v'VVV";
                    options.SubstituteApiVersionInUrl = true;
                });

            builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Limits.MaxRequestBodySize = MaxRequestBodyBytes;
            kestrel.AddServerHeader = false;
        });

            builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

            builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier));
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
            builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

            var allowedOrigins = builder.Environment.IsDevelopment()
                ? ["http://localhost:7200", "https://localhost:7201"]
                : builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            builder.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

            var knownProxyNetworks = builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [];
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                foreach (var network in knownProxyNetworks)
                    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            });

            return builder;
        }
    }
}