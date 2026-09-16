using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using TransactionAggregation.Application;
using TransactionAggregation.Infrastructure;
using TransactionAggregation.Persistence;
using TransactionAggregationAPI;
using TransactionAggregationAPI.Endpoints;
using TransactionAggregationAPI.Extensions;
using TransactionAggregationAPI.Middleware;
using TransactionAggregationAPI.RateLimiting;
using Prometheus;

try
{
    var builder = WebApplication.CreateBuilder(args);


    builder.Logging.ClearProviders();

    builder.AddServiceDefaults();

    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
        .CreateLogger();

    
    builder.Logging.AddSerilog(Log.Logger, dispose: true);

    builder.Services.AddSingleton<Serilog.ILogger>(Log.Logger);
    builder.Services.AddSingleton<Serilog.Extensions.Hosting.DiagnosticContext>();
    builder.Services.AddSingleton<Serilog.IDiagnosticContext>(
        sp => sp.GetRequiredService<Serilog.Extensions.Hosting.DiagnosticContext>());

    var connectionString = builder.Configuration.GetConnectionString("transactiondb");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly("TransactionAggregation.Persistence");
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorCodesToAdd: null);
        });

        if (builder.Environment.IsDevelopment())
        {
            options.EnableDetailedErrors();
            options.EnableSensitiveDataLogging();
        }
    });
    
    builder.EnrichNpgsqlDbContext<ApplicationDbContext>();

    builder.AddRedisClient("redis", configureSettings: s => s.DisableHealthChecks = true);

    // Bank-link OAuth tokens are encrypted with Data Protection before being persisted (see
    // BankLinkCredentialProtector). The key ring itself must be shared across replicas and
    // survive pod restarts, or tokens encrypted by one pod become undecryptable after a
    // restart/rolling deploy — so it's persisted to Redis rather than the per-machine default.
    // A dedicated connection (rather than the shared IConnectionMultiplexer above) sidesteps
    // DI-ordering issues, since Data Protection needs a live connection at registration time.
    var redisConnectionString = builder.Configuration.GetConnectionString("redis");
    if (!string.IsNullOrEmpty(redisConnectionString))
    {
        try
        {
            // AbortOnConnectFail = false (matching REDIS_CONNECTION_STRING elsewhere in this
            // app) so a Redis that's briefly unreachable at startup doesn't crash the whole
            // API — this is a bolt-on feature, not core to auth working. Data Protection will
            // just use ephemeral keys until Redis becomes reachable.
            var dpRedisOptions = ConfigurationOptions.Parse(redisConnectionString);
            dpRedisOptions.AbortOnConnectFail = false;

            builder.Services.AddDataProtection()
                .SetApplicationName("TransactionAggregationAPI")
                .PersistKeysToStackExchangeRedis(
                    ConnectionMultiplexer.Connect(dpRedisOptions),
                    "DataProtection-Keys");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not set up Redis-backed Data Protection key storage — falling back to ephemeral keys for this instance. Bank-link tokens encrypted before this is fixed may become unrecoverable across restarts/replicas.");
        }
    }
    else
    {
        Log.Warning("No Redis connection string configured — Data Protection keys will not survive a restart or be shared across replicas. Bank-link tokens encrypted before this is fixed will become unrecoverable.");
    }

    builder.Services.AddApplication(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddPersistence();

    // Keycloak is the sole identity provider — the API only validates the tokens it issues,
    // it never signs its own. Fail fast at startup if any of this is missing rather than
    // booting with an auth pipeline that will reject every request.
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
            // MetadataAddress (not Authority) is set explicitly so the API fetches Keycloak's
            // signing keys from the internal/in-cluster address — reliable, no dependency on
            // host DNS — while still validating each token's `iss` claim against PublicIssuer,
            // the externally-reachable URL the browser actually used to sign in. These two
            // addresses point at the same realm but are rarely the same string in
            // docker-compose/k8s, where the API and the browser reach Keycloak differently.
            options.MetadataAddress =
                $"{keycloakAuthority.TrimEnd('/')}/realms/{keycloakRealm}/.well-known/openid-configuration";
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

            // Keep Keycloak's raw claim names ("sub", "preferred_username", ...) instead of
            // ASP.NET Core's legacy inbound remapping to long XML-namespaced ClaimTypes.
            options.MapInboundClaims = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = keycloakPublicIssuer,
                ValidAudience = keycloakAudience,
                // Keycloak realm roles arrive as a "roles" claim (see the transaction-ui
                // client's realm-role protocol mapper in keycloak/realm-export.json) — a
                // JSON-array-valued claim, which the JWT handler already expands into one
                // Claim("roles", <role>) per entry. Naming it here is what makes
                // RequireRole/[Authorize(Roles=...)] read the right claim.
                RoleClaimType = "roles"
            };
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("Admin", policy => policy.RequireRole("admin"));
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
    builder.Services.AddOpenApi();

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
                // Production origins come from configuration (Cors:AllowedOrigins), not a
                // hardcoded localhost list — the previous "DevCors" policy was applied
                // unconditionally, which meant CORS silently never allowed the real,
                // deployed SPA origin.
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
        options.AddPolicy<string, RedisFixedWindowPolicy>("FixedWindow");
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

   
    // The app always sits behind a reverse proxy (nginx in front of the UI container in
    // docker-compose; the Traefik ingress in k8s), so Connection.RemoteIpAddress is the
    // proxy's IP, not the client's, unless we read X-Forwarded-For/-Proto. Without this,
    // per-IP rate limiting collapses all anonymous traffic behind the proxy into one bucket.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // The proxy is always the previous hop inside the same docker/k8s network, not an
        // arbitrary internet host, so trust it unconditionally rather than maintaining an
        // explicit allow-list of proxy IPs that changes with every pod restart.
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    builder.Services.AddOptions<RateLimiterOptions>()
        .Configure<IConnectionMultiplexer, ILoggerFactory>((options, redis, loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger<RedisFixedWindowRateLimiter>();

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                context =>
                {
                    // See RedisFixedWindowPolicy for why the "sub" claim (not Identity.Name)
                    // is the correct authenticated-user key here.
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

    // Must run first so RemoteIpAddress/Scheme are corrected before anything downstream
    // (rate limiting, HTTPS redirection) reads them.
    app.UseForwardedHeaders();

    // Must run before UseBlazorFrameworkFiles / UseStaticFiles so that Blazor's
    // MapFallbackToFile("index.html") cannot intercept the /metrics path first.
    app.UseMetricServer();
    app.UseHttpMetrics();

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

    app.MapCustomerEndpoints();
    app.MapTransactionEndpoints();
    app.MapAccountEndpoints();
    app.MapBankLinkEndpoints();
    app.MapWebhookEndpoints();
    app.MapWebhookSourceEndpoints();

    app.MapFallbackToFile("index.html");

    // --migrate-only: apply pending migrations and exit with code 0.
    // Used by the Kubernetes pre-deploy Job (k8s/api/migration-job.yaml) so migrations
    // are applied once before any API replica starts, instead of every replica racing to
    // migrate on its own startup. This was previously commented out, which meant the Job's
    // `dotnet TransactionAggregationAPI.dll --migrate-only` command silently ignored the
    // flag and started the full web server instead of exiting — the Job would just hang
    // until activeDeadlineSeconds and fail.
    if (args.Contains("--migrate-only"))
    {
        await app.ApplyMigrationsAsync();
        return;
    }

    // ApplyMigrationsAsync() also runs on normal startup (needed for docker-compose, which
    // has no separate migration Job) — it's now safe to call from multiple replicas
    // concurrently because it takes a Postgres advisory lock internally (see
    // MigrationExtensions.ApplyMigrationsAsync).
    await app.ApplyMigrationsAsync();

    await SeedData.SeedDatabaseAsync(app.Services);
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

// Expose Program to the integration test assembly
public partial class Program { }