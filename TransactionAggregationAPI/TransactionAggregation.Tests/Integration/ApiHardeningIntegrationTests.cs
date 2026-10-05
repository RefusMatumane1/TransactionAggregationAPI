using BuildingBlocks.Messaging.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.WebhookSources.Application.Persistence;
using Modules.WebhookSources.Domain;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    public class ApiHardeningIntegrationTests : IClassFixture<IntegrationTestWebAppFactory>
    {
        private const string WebhookPath = "/api/v1/webhooks/bank-aggregator/transactions";

        private readonly IntegrationTestWebAppFactory _factory;
        private readonly HttpClient _client;

        public ApiHardeningIntegrationTests(IntegrationTestWebAppFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<string> SeedSourceAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IWebhookSourcesDbContext>();
            var (source, apiKey) = WebhookSource.Create($"hardening-{Guid.NewGuid():N}", $"hardening-{Guid.NewGuid():N}", "#123456");
            context.WebhookSources.Add(source);
            await context.SaveChangesAsync();
            return apiKey;
        }

        private static HttpRequestMessage Webhook(string body, string apiKey, string? correlationId = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, WebhookPath)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Api-Key", apiKey);
            if (correlationId is not null)
                request.Headers.Add("X-Correlation-Id", correlationId);
            return request;
        }

        private static HttpRequestMessage AsStaff(HttpMethod method, string path)
        {
            var request = new HttpRequestMessage(method, path);
            request.Headers.Add(TestAuthHandler.UserIdHeaderName, Guid.NewGuid().ToString());
            request.Headers.Add(TestAuthHandler.RolesHeaderName, "staff");
            return request;
        }

        private static async Task<JsonElement> ProblemAsync(HttpResponseMessage response)
        {
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var problem = document.RootElement.Clone();
            problem.GetProperty("status").GetInt32().Should().Be((int)response.StatusCode);
            problem.TryGetProperty("traceId", out _).Should().BeTrue("every error carries the id support can look up");
            return problem;
        }

        [Fact]
        public async Task MalformedJson_Returns400ProblemDetails_NotAnEmptyBody()
        {
            var response = await _client.SendAsync(Webhook("{\"externalAccountId\": \"x\", \"transactions\": [ {", await SeedSourceAsync()));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await ProblemAsync(response);
        }

        [Fact]
        public async Task Unauthenticated_Returns401ProblemDetails()
        {
            var response = await _client.GetAsync("/api/v1/transactions");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            await ProblemAsync(response);
        }

        [Fact]
        public async Task ApiResponses_CarrySecurityHeaders()
        {
            var response = await _client.SendAsync(AsStaff(HttpMethod.Get, "/api/v1/transactions"));

            response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle("nosniff");
            response.Headers.GetValues("X-Frame-Options").Should().ContainSingle("DENY");
            response.Headers.GetValues("Referrer-Policy").Should().ContainSingle("no-referrer");
            response.Content.Headers.TryGetValues("Content-Security-Policy", out _).Should().BeFalse();
            response.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("default-src 'none'");
            response.Headers.CacheControl!.NoStore.Should().BeTrue("financial data must not be cached by intermediaries");
        }

        [Theory]
        [InlineData("pageSize=1000", "pageSize")]
        [InlineData("cursor=not-a-cursor&pageSize=20", "cursor")]
        [InlineData("pageSize=20&fromDate=2026-02-01&toDate=2026-01-01", "fromDate")]
        public async Task TransactionList_RejectsUnboundedOrInvalidPaging(string query, string field)
        {
            var response = await _client.SendAsync(AsStaff(HttpMethod.Get, $"/api/v1/transactions?{query}"));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var problem = await ProblemAsync(response);
            problem.GetProperty("errors").EnumerateObject().Select(p => p.Name)
                .Should().Contain(name => name.Equals(field, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task Summary_RejectsAnInvertedPeriod()
        {
            var response = await _client.SendAsync(AsStaff(HttpMethod.Get,
                "/api/v1/transactions/summary?startDate=2026-06-01&endDate=2026-01-01"));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await ProblemAsync(response);
        }

        [Fact]
        public async Task Webhook_NulCharacter_IsRejectedAt400_NeverAcceptedAndLeftToFail()
        {
            var body = """{"externalAccountId":"acc-nul","transactions":[{"id":"t-nul","amount":-10,"currency":"ZAR","description":"CAFE\u0000X","date":"2026-09-10T12:00:00Z"}]}""";

            var response = await _client.SendAsync(Webhook(body, await SeedSourceAsync()));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await ProblemAsync(response)).GetProperty("errors").EnumerateObject().Select(p => p.Name)
                .Should().Contain("transactions[0].description");
        }

        [Fact]
        public async Task Webhook_CarriesTheRequestsCorrelationId_OntoTheInboxRow()
        {
            var correlationId = $"corr-{Guid.NewGuid():N}";
            var body = $$"""{"externalAccountId":"acc-corr","transactions":[{"id":"t-{{Guid.NewGuid():N}}","amount":-10,"currency":"ZAR","description":"Coffee","date":"2026-09-10T12:00:00Z"}]}""";

            var response = await _client.SendAsync(Webhook(body, await SeedSourceAsync(), correlationId));

            response.StatusCode.Should().Be(HttpStatusCode.Accepted);
            response.Headers.GetValues("X-Correlation-Id").Should().ContainSingle(correlationId);
            using var scope = _factory.Services.CreateScope();
            var messaging = scope.ServiceProvider.GetRequiredService<IMessagingDbContext>();
            (await messaging.InboxMessages.AsNoTracking().Where(m => m.CorrelationId == correlationId).CountAsync())
                .Should().Be(1, "the worker restores this id, so its logs join the request that received the delivery");
        }
    }
}