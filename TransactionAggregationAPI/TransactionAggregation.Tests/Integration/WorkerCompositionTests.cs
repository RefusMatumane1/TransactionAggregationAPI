using BuildingBlocks.Application.Abstractions.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Modules.Reporting.Application.Aggregation;
using Modules.Transactions.Application.Common.Interfaces;
using TransactionAggregation.Hosting;
using TransactionAggregation.Worker;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    /// <summary>
    /// The worker is assembled from the same modules as the API plus its own processing. Nothing else builds that container, so a
    /// registration lost in a refactor would otherwise surface only when a worker pod starts.
    /// </summary>
    public class WorkerCompositionTests
    {
        private static WebApplication BuildWorker()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", ContentRootPath = AppContext.BaseDirectory });
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:transactiondb"] = "Host=localhost;Database=test",
                ["ConnectionStrings:redis"] = "localhost:6379",
                ["ConnectionStrings:kafka"] = "localhost:9092",
                ["Aspire:Redis:DisableHealthChecks"] = "true"
            });
            builder.Host.UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            });

            builder.AddApplicationModules();
            builder.Services.AddScoped<IUserContext, BackgroundUserContext>();
            builder.Services.AddBackgroundProcessing(builder.Configuration);

            return builder.Build();
        }

        [Fact]
        public void TheWorkersContainer_ResolvesEverythingItRegisters()
        {
            var act = BuildWorker;

            act.Should().NotThrow("ValidateOnBuild resolves every registration, hosted services included");
        }

        [Fact]
        public void EachJobOfTheWorker_CanBeBuiltInItsOwnScope()
        {
            using var worker = BuildWorker();
            using var scope = worker.Services.CreateScope();

            scope.ServiceProvider.GetRequiredService<IDailyTotalsRefresher>().Should().NotBeNull("the totals job needs the Reporting module");
            scope.ServiceProvider.GetRequiredService<ITransactionsDbContext>().Should().NotBeNull("the inbox job needs the ledger");
        }
    }
}
