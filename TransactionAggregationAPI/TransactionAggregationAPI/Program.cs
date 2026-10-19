using BuildingBlocks.Application.Seeding;
using BuildingBlocks.Persistence.Migrations;
using Modules.Audit;
using Modules.Currency;
using Modules.Customers;
using Modules.Notifications;
using Modules.Reporting;
using Modules.Transactions;
using Modules.WebhookSources;
using Prometheus;
using Scalar.AspNetCore;
using Serilog;
using TransactionAggregation.Hosting;
using TransactionAggregation.Hosting.Observability;
using TransactionAggregationAPI.Authentication;
using TransactionAggregationAPI.Extensions;
using TransactionAggregationAPI.Middleware;
using TransactionAggregationAPI.RateLimiting;

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.AddSecretsFile();

    builder.Logging.ClearProviders();
    builder.AddServiceDefaults();
    builder.AddSerilogLogging();

    builder.AddApplicationModules();
    builder.Services.AddCustomersModule(builder.Environment.IsDevelopment());
    builder.AddKeycloakAuthentication();
    builder.AddApiPlatform();
    builder.Services.AddRedisRateLimiting();

    var app = builder.Build();

    app.UseForwardedHeaders();

    app.UseMiddleware<RequestContextLoggingMiddleware>();
    app.UseExceptionHandler();

    app.UseStatusCodePages();
    app.UseMiddleware<SecurityHeadersMiddleware>();

    app.UsePrivateMetricsEndpoint();
    app.UseHttpMetrics();

    if (!app.Environment.IsDevelopment())
        app.UseHsts();

    app.UseHttpsRedirection();
    app.UseResponseCompression();

    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();

    app.UseCors(ApiPlatformSetup.CorsPolicyName);

    app.UseRouteTemplateRequestLogging();

    app.UseMiddleware<AuthFailureThrottleMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapDefaultEndpoints();

    var openApi = app.MapOpenApi();
    if (app.Environment.IsDevelopment())
        app.MapScalarApiReference();
    else
        openApi.RequireAuthorization();

    app.MapCurrencyEndpoints();
    app.MapTransactionsEndpoints();
    app.MapReportingEndpoints();
    app.MapWebhookSourcesEndpoints();
    app.MapAuditEndpoints();
    app.MapCustomersEndpoints();
    app.MapNotificationsEndpoints();

    app.MapFallback("api/{**path}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound));
    app.MapFallbackToFile("index.html");

    if (args.Contains("--migrate-only"))
    {
        await app.Services.ApplyModuleMigrationsAsync();
        return;
    }

    if (app.Environment.IsDevelopment())
    {
        await app.Services.ApplyModuleMigrationsAsync();
        await app.Services.SeedDevelopmentDataAsync();
    }

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application start-up failed");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }