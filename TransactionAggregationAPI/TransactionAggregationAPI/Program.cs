using BuildingBlocks.Messaging.Persistence;
using BuildingBlocks.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Modules.Audit;
using Modules.Audit.Infrastructure.Persistence;
using Modules.BankLinks;
using Modules.BankLinks.Infrastructure.Persistence;
using Modules.Customers;
using Modules.Customers.Infrastructure.Persistence;
using Modules.Transactions;
using Modules.Transactions.Infrastructure.Persistence;
using Modules.WebhookSources;
using Modules.WebhookSources.Infrastructure.Persistence;
using Prometheus;
using Scalar.AspNetCore;
using Serilog;
using SharedKernel.Abstractions.Authentication;
using StackExchange.Redis;
using System.Security.Claims;
using System.Threading.RateLimiting;
using TransactionAggregation.Hosting;
using TransactionAggregationAPI;
using TransactionAggregationAPI.Authentication;
using TransactionAggregationAPI.Extensions;
using TransactionAggregationAPI.Middleware;
using TransactionAggregationAPI.RateLimiting;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders();

    builder.AddServiceDefaults();
    builder.AddSerilogLogging();

    // Background processing (Kafka consumer, inbox/outbox/pending-expiry dispatchers) runs
    // in TransactionAggregation.Worker, not here — this host only serves HTTP.
    builder.AddApplicationModules();

    builder.Services.AddScoped<IUserContext, UserContext>();

    var keycloakAuthority = builder.Configuration["Keycloak:Authority"];
    var keycloakRealm = builder.Configuration["Keycloak:Realm"];
    var keycloakPublicIssuer = builder.Configuration["Keycloak:PublicIssuer"];
    var keycloakAudience = builder.Configuration["Keycloak:Audience"];
    if (string.IsNullOrEmpty(keycloakAuthority) || string.IsNullOrEmpty(keycloakRealm) ||
        string.IsNullOrEmpty(keycloakPublicIssuer) || string.IsNullOrEmpty(keycloakAudience))
        throw new InvalidOperationException(
            "Keycloak:Authority, Keycloak:Realm, Keycloak:PublicIssuer and Keycloak:Audience must all be configured.");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {

            options.MetadataAddress =
                            $"{keycloakAuthority.TrimEnd('/')}/realms/{keycloakRealm}/.well-known/openid-configuration";

            // Decoupled from IsDevelopment() on purpose: whether this specific
            // in-cluster hop needs HTTPS is an infrastructure fact (is there a TLS
            // listener on the other end?), not a proxy for "is this environment
            // Production." The in-cluster Keycloak service currently serves plain
            // HTTP (see k8s/keycloak — KC_HTTP_ENABLED), with TLS terminated at the
            // Ingress for external traffic only; NetworkPolicy isolates the
            // namespace. Explicitly set Keycloak:RequireHttpsMetadata=false in that
            // ConfigMap to reflect that reviewed decision — defaulting here to
            // "true unless Development" only when the value isn't set at all, so
            // this doesn't silently downgrade security for anyone who hasn't set it.
            options.RequireHttpsMetadata = builder.Configuration.GetValue<bool?>("Keycloak:RequireHttpsMetadata")
                ?? !builder.Environment.IsDevelopment();

            options.MapInboundClaims = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = keycloakPublicIssuer,
                ValidAudience = keycloakAudience,

                RoleClaimType = "roles"
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(AuthorizationPolicies.Admin, policy => policy.RequireRole("admin"));
    });

    builder.Services.AddResponseCaching();
    builder.Services.AddHttpContextAccessor();

    builder.Services.AddApiVersioning(options =>
    {
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
        options.ReportApiVersions = true;
    }).AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

    builder.Services.AddControllers();
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    });
    builder.Services.AddProblemDetails();

    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
    });

    const string CorsPolicyName = "Default";
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(CorsPolicyName, policy =>
        {
            if (builder.Environment.IsDevelopment())
            {
                policy.WithOrigins("http://localhost:7200", "https://localhost:7201")
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            }
            else
            {

                var allowedOrigins = builder.Configuration
                                    .GetSection("Cors:AllowedOrigins")
                                    .Get<string[]>() ?? [];

                policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            }
        });
    });

    builder.Services.AddSingleton<RedisFixedWindowPolicy>();
    builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy<string, RedisFixedWindowPolicy>(RateLimitPolicies.FixedWindow);
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    // X-Forwarded-For is only honoured from the reverse proxies listed in
    // ForwardedHeaders:KnownNetworks (the ingress controller's pod CIDR in k8s). Trusting
    // it from anyone let a client pick its own "remote IP" — and so a fresh anonymous
    // rate-limit partition — per request. With nothing configured only loopback is trusted.
    var knownProxyNetworks = builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [];
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            foreach (var network in knownProxyNetworks)
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        });

    builder.Services.AddOptions<RateLimiterOptions>()
        .Configure<IConnectionMultiplexer, ILoggerFactory>((options, redis, loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger<RedisFixedWindowRateLimiter>();

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                context =>
                {

                    var key = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                                            ?? context.Connection.RemoteIpAddress?.ToString()
                                            ?? "anonymous";

                    return RateLimitPartition.Get<string>(
                        key,
                        partitionKey => new RedisFixedWindowRateLimiter(
                            redis,
                            $"ratelimit:global:{partitionKey}",
                            new RedisRateLimiterOptions
                            {
                                PermitLimit = 100,
                                Window = TimeSpan.FromMinutes(1),
                                AllowRequestOnRedisFailure = true
                            },
                            logger));
                });
        });

    var app = builder.Build();

    app.MapDefaultEndpoints();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseForwardedHeaders();

    app.UseMetricServer();
    app.UseHttpMetrics();

    if (!app.Environment.IsDevelopment())
        app.UseHsts();

    app.UseHttpsRedirection();
    app.UseResponseCompression();

    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();

    app.UseResponseCaching();

    app.UseCors(CorsPolicyName);

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseRequestContextLogging();

    app.UseSerilogRequestLogging();

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseRateLimiter();

    app.MapCustomersEndpoints();
    app.MapTransactionsEndpoints();
    app.MapBankLinksEndpoints();
    app.MapWebhookSourcesEndpoints();
    app.MapAuditEndpoints();

    app.MapFallbackToFile("index.html");

    // Each module's own DbContext owns its own schema/migration history — see
    // docs/adr/0009-schema-per-module-database-strategy.md — so each applies
    // independently. Order doesn't matter: no schema references another's tables.
    static async Task ApplyAllMigrationsAsync(WebApplication app)
    {
        await app.ApplyMigrationsAsync<TransactionsDbContext>();
        await app.ApplyMigrationsAsync<MessagingDbContext>();
        await app.ApplyMigrationsAsync<WebhookSourcesDbContext>();
        await app.ApplyMigrationsAsync<BankLinksDbContext>();
        await app.ApplyMigrationsAsync<CustomersDbContext>();
        await app.ApplyMigrationsAsync<AuditDbContext>();
    }

    if (args.Contains("--migrate-only"))
    {
        await ApplyAllMigrationsAsync(app);
        return;
    }

    // Staging/Production apply migrations via the dedicated db-migrate Job
    // (k8s/api/migration-job.yaml, run with --migrate-only above) before this
    // Deployment rolls out — see the comment in k8s/api/deployment.yaml explaining
    // why no pod self-migrates there. Auto-migrating and seeding demo data here is
    // a Development-only convenience for docker-compose/local dev, which has no
    // separate migration step. Never seed demo customers (fake accounts with a
    // well-known password) into a real environment.
    if (app.Environment.IsDevelopment())
    {
        await ApplyAllMigrationsAsync(app);
        await SeedData.SeedDatabaseAsync(app.Services);
        await MockAggregatorSource.EnsureRegisteredAsync(app.Services, app.Configuration);
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }