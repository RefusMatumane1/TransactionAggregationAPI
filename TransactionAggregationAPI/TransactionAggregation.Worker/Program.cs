using Modules.Transactions;
using Prometheus;
using Serilog;
using SharedKernel.Abstractions.Authentication;
using TransactionAggregation.Hosting;
using TransactionAggregation.Worker;

// Background processing for the Transactions module: the Kafka consumer and the inbox,
// outbox and pending-expiry dispatchers. Runs as its own process so it scales
// independently of the HTTP API; every dispatcher claims work through Postgres row
// claims, so any number of replicas is safe. Migrations are not applied here — the API
// (Development) or the db-migrate Job (k8s) owns them; until they exist the dispatchers
// log the failure and retry on their next poll.
try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders();

    builder.AddServiceDefaults();
    builder.AddSerilogLogging();

    builder.AddApplicationModules();
    builder.Services.AddScoped<IUserContext, BackgroundUserContext>();

    builder.Services.AddTransactionsBackgroundProcessing(builder.Configuration);

    var app = builder.Build();

    app.MapDefaultEndpoints();
    app.UseMetricServer();

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker start-up failed");
    throw;
}
finally
{
    Log.CloseAndFlush();
}