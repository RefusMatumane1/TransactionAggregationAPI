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
using Npgsql;
using StackExchange.Redis;
using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.Persistence;
using Modules.WebhookSources;
using Modules.WebhookSources.Persistence;
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
        .Destructure.With<TransactionAggregationAPI.Logging.SensitiveDataDestructuringPolicy>()
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
        .CreateLogger();

    builder.Logging.AddSerilog(Log.Logger, dispose: true);

    builder.Services.AddSingleton<Serilog.ILogger>(Log.Logger);
    builder.Services.AddSingleton<Serilog.Extensions.Hosting.DiagnosticContext>();
    builder.Services.AddSingleton<Serilog.IDiagnosticContext>(
        sp => sp.GetRequiredService<Serilog.Extensions.Hosting.DiagnosticContext>());

    var connectionString = builder.Configuration.GetConnectionString("transactiondb");

    // ApplicationDbContext and MessagingDbContext share one scoped NpgsqlConnection
    // (rather than each opening its own) so ApplicationDbContext.SaveChangesAsync can
    // share one real transaction across both — required for an Outbox write to commit
    // atomically with the business-entity change that triggered it. See
    // docs/adr/0009-schema-per-module-database-strategy.md. WebhookSourcesDbContext
    // has no such requirement (every write there is a standalone unit of work) and
    // keeps its own independent connection, registered inside AddWebhookSourcesModule.
    builder.Services.AddScoped(_ => new NpgsqlConnection(connectionString));

    builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    {
        options.UseNpgsql(sp.GetRequiredService<NpgsqlConnection>(), npgsqlOptions =>
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

    builder.Services.AddMessagingBuildingBlock(builder.Configuration);
    builder.Services.AddWebhookSourcesModule(builder.Configuration);

    builder.AddRedisClient("redis", configureSettings: s => s.DisableHealthChecks = true);

    var redisConnectionString = builder.Configuration.GetConnectionString("redis");
    if (!string.IsNullOrEmpty(redisConnectionString))
    {
        try
        {

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

            // The log message above used to be a lie: nothing actually configured a
            // fallback, so Data Protection silently fell through to ASP.NET Core's
            // implicit default — which, in a container without a stable user profile,
            // is ambiguous and may try (and fail) to write to a local file path.
            // UseEphemeralDataProtectionProvider() makes the fallback explicit and
            // matches what was already being claimed in the log.
            builder.Services.AddDataProtection()
                .SetApplicationName("TransactionAggregationAPI")
                .UseEphemeralDataProtectionProvider();
        }
    }
    else
    {
        Log.Warning("No Redis connection string configured — Data Protection keys will not survive a restart or be shared across replicas. Bank-link tokens encrypted before this is fixed will become unrecoverable.");

        builder.Services.AddDataProtection()
            .SetApplicationName("TransactionAggregationAPI")
            .UseEphemeralDataProtectionProvider();
    }

    builder.Services.AddApplication(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddPersistence();

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
        options.AddPolicy<string, RedisFixedWindowPolicy>("FixedWindow");
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

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

    app.MapCustomerEndpoints();
    app.MapTransactionEndpoints();
    app.MapAccountEndpoints();
    app.MapBankLinkEndpoints();
    app.MapWebhookEndpoints();
    app.MapWebhookSourceEndpoints();

    app.MapFallbackToFile("index.html");

    // Each module's own DbContext owns its own schema/migration history — see
    // docs/adr/0009-schema-per-module-database-strategy.md — so each applies
    // independently. Order doesn't matter: no schema references another's tables.
    static async Task ApplyAllMigrationsAsync(WebApplication app)
    {
        await app.ApplyMigrationsAsync<ApplicationDbContext>();
        await app.ApplyMigrationsAsync<MessagingDbContext>();
        await app.ApplyMigrationsAsync<WebhookSourcesDbContext>();
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