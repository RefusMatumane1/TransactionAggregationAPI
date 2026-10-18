using BuildingBlocks.Application.Abstractions.Authentication;
using Serilog;
using TransactionAggregation.Hosting;
using TransactionAggregation.Hosting.Observability;

namespace TransactionAggregation.Worker
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            try
            {
                var builder = WebApplication.CreateBuilder(args);
                builder.AddSecretsFile();

                builder.Logging.ClearProviders();

                builder.AddServiceDefaults();
                builder.AddSerilogLogging();

                builder.AddApplicationModules();
                builder.Services.AddScoped<IUserContext, BackgroundUserContext>();

                builder.Services.AddBackgroundProcessing(builder.Configuration);

                var app = builder.Build();

                app.MapDefaultEndpoints();
                app.UsePrivateMetricsEndpoint();

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
        }
    }
}