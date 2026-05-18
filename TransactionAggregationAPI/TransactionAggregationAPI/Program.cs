using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using System.Text;
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

    builder.Services.AddApplication(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddPersistence();

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
            };
        });
    
    builder.Services.AddAuthorization();

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

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DevCors", policy =>
            policy.WithOrigins("http://localhost:7200", "https://localhost:7201")
                  .AllowAnyHeader()
                  .AllowAnyMethod());
    });

    builder.Services.AddSingleton<RedisFixedWindowPolicy>();
    builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy<string, RedisFixedWindowPolicy>("FixedWindow");
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

   
    builder.Services.AddOptions<RateLimiterOptions>()
        .Configure<IConnectionMultiplexer>((options, redis) =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                context =>
                {
                    var key = context.User.Identity?.Name
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
                            }));
                });
        });

    var app = builder.Build();

    app.MapDefaultEndpoints();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    // Must run before UseBlazorFrameworkFiles / UseStaticFiles so that Blazor's
    // MapFallbackToFile("index.html") cannot intercept the /metrics path first.
    app.UseMetricServer();
    app.UseHttpMetrics();

    app.UseHttpsRedirection();

    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();

    app.UseResponseCaching();

    app.UseCors("DevCors");

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseRequestContextLogging();

    app.UseSerilogRequestLogging();

    app.UseExceptionHandler();

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseRateLimiter();

    app.MapCustomerEndpoints();
    app.MapTransactionEndpoints();
    app.MapAccountEndpoints();

    app.MapFallbackToFile("index.html");

    // --migrate-only: apply pending migrations and exit with code 0.
    // Used exclusively by the Kubernetes pre-deploy Job (k8s/api/migration-job.yaml)
    // so that exactly one process runs migrations before any API replica starts.
    /*if (args.Contains("--migrate-only"))
    {
        await app.ApplyMigrationsAsync();
        return;
    }*/
    
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