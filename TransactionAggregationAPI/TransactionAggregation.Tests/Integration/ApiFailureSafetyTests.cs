using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Modules.Transactions.Application.Common.Interfaces;
using Modules.Transactions.Domain.Entities;
using Npgsql;
using System.Net;
using System.Text.Json;
using TransactionAggregation.Hosting.Health;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    public class ApiFailureSafetyTests(IntegrationTestWebAppFactory factory) : IClassFixture<IntegrationTestWebAppFactory>
    {
        [Theory]
        [InlineData("searchTerm=%27%3B%20DROP%20TABLE%20transactions.%22Transactions%22%3B%20--")]
        [InlineData("searchTerm=%25%25%25%25")]
        [InlineData("institution=%27%20OR%201%3D1%20--")]
        [InlineData("sortBy=Date%3B%20DELETE%20FROM%20x")]
        [InlineData("cursor=%27%20OR%20%271%27%3D%271")]
        public async Task InjectionPayloads_AreAnsweredSafely_NeverWithAServerError(string query)
        {
            using var admin = factory.CreateClient().SignedInAs("admin");

            var response = await admin.GetAsync($"/api/v1/transactions?{query}");

            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().NotContainAny("Npgsql", "SQL", "Exception", "   at ");
        }

        [Fact]
        public async Task AnUnexpectedFailure_ReturnsAProblemDetails_WithATraceId_AndNoInternals()
        {
            using var failing = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITransactionSearch>();
                services.AddSingleton<ITransactionSearch, ExplodingSearch>();
            }));
            using var admin = failing.CreateClient().SignedInAs("admin");

            var response = await admin.GetAsync("/api/v1/transactions?searchTerm=coffee");
            var body = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
            var problem = JsonDocument.Parse(body).RootElement;
            problem.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
            body.Should().NotContainAny(ExplodingSearch.Secret, nameof(ExplodingSearch), "InvalidOperationException", "   at ", ".cs:line");
        }

        [Fact]
        public async Task DatabaseUnreachable_ReadinessFails_WhileLivenessStaysHealthy()
        {
            using var noDatabase = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.Configure<HealthCheckServiceOptions>(options => options.Registrations.Clear());
                services.AddHealthChecks()
                    .AddCheck("self", () => HealthCheckResult.Healthy(), [Microsoft.Extensions.Hosting.Extensions.LiveTag])
                    .AddCheck<PostgresHealthCheck>("postgres", HealthStatus.Unhealthy, [Microsoft.Extensions.Hosting.Extensions.ReadyTag]);
                services.RemoveAll<NpgsqlConnection>();
                services.AddScoped(_ => new NpgsqlConnection("Host=127.0.0.1;Port=1;Timeout=2;Database=unreachable"));
            }));
            using var client = noDatabase.CreateClient();

            var readiness = await client.GetAsync("/readiness");
            var liveness = await client.GetAsync("/liveness");

            readiness.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, "traffic must stop flowing to a pod that cannot reach its database");
            (await readiness.Content.ReadAsStringAsync()).Should().Be("Unhealthy");
            liveness.StatusCode.Should().Be(HttpStatusCode.OK, "a restart would not bring the database back");
        }

        private sealed class ExplodingSearch : ITransactionSearch
        {
            public const string Secret = "Host=db;Password=hunter2";

            public IQueryable<Transaction> DescriptionContains(IQueryable<Transaction> transactions, string term) =>
                throw new InvalidOperationException($"connection failed: {Secret}");
        }
    }
}