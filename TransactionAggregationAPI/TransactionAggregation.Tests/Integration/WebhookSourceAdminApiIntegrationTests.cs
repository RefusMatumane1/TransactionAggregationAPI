using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    public class WebhookSourceAdminApiIntegrationTests : IClassFixture<IntegrationTestWebAppFactory>
    {
        private const string BasePath = "/api/v1/admin/webhook-sources";
        private const string WebhookPath = "/api/v1/webhooks/bank-aggregator/transactions";

        private readonly IntegrationTestWebAppFactory _factory;
        private readonly HttpClient _client;

        public WebhookSourceAdminApiIntegrationTests(IntegrationTestWebAppFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private record CreateResponse(Guid Id, string Name, string ApiKey);
        private record RotateResponse(string ApiKey);

        private HttpClient AsAdmin()
        {
            // A fresh client from the same in-memory test server (not `new HttpClient()` — that
            // would try a real TCP connection to the test server's placeholder BaseAddress).
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeaderName, "admin");
            return client;
        }

        // ── Authorization ─────────────────────────────────────────────────────

        [Fact]
        public async Task GetSources_Anonymous_Returns401()
        {
            var response = await _client.GetAsync(BasePath);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetSources_AuthenticatedNonAdmin_Returns403()
        {
            using var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, Guid.NewGuid().ToString());

            var response = await client.GetAsync(BasePath);

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        [Fact]
        public async Task CreateRotateDeactivate_FullLifecycle_Works()
        {
            using var admin = AsAdmin();

            // Create
            var createResponse = await admin.PostAsJsonAsync(BasePath, new { Name = $"lifecycle-{Guid.NewGuid()}" });
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var created = await createResponse.Content.ReadFromJsonAsync<CreateResponse>();
            created.Should().NotBeNull();
            created!.ApiKey.Should().NotBeNullOrWhiteSpace();

            // The new key works on the transactions webhook (auth-wise — BankLink resolution now
            // happens asynchronously, so a valid key always gets 202 regardless of whether
            // "never-linked" resolves to anything; 401 is what a rejected key looks like).
            var firstWebhookCall = await PostWebhookAsync(created.ApiKey);
            firstWebhookCall.StatusCode.Should().Be(HttpStatusCode.Accepted);

            // Rotate
            var rotateResponse = await admin.PostAsync($"{BasePath}/{created.Id}/rotate", null);
            rotateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var rotated = await rotateResponse.Content.ReadFromJsonAsync<RotateResponse>();
            rotated!.ApiKey.Should().NotBe(created.ApiKey);

            // Old key no longer works, new one does
            (await PostWebhookAsync(created.ApiKey)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await PostWebhookAsync(rotated.ApiKey)).StatusCode.Should().Be(HttpStatusCode.Accepted);

            // Deactivate
            var deactivateResponse = await admin.PostAsync($"{BasePath}/{created.Id}/deactivate", null);
            deactivateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            (await PostWebhookAsync(rotated.ApiKey)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            // Reactivate
            var activateResponse = await admin.PostAsync($"{BasePath}/{created.Id}/activate", null);
            activateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            (await PostWebhookAsync(rotated.ApiKey)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        [Fact]
        public async Task CreateSource_DuplicateName_Returns409()
        {
            using var admin = AsAdmin();
            var name = $"dup-{Guid.NewGuid()}";

            await admin.PostAsJsonAsync(BasePath, new { Name = name });
            var response = await admin.PostAsJsonAsync(BasePath, new { Name = name });

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task RotateUnknownSource_Returns404()
        {
            using var admin = AsAdmin();

            var response = await admin.PostAsync($"{BasePath}/{Guid.NewGuid()}/rotate", null);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetSources_ListsCreatedSourceWithoutKeyMaterial()
        {
            using var admin = AsAdmin();
            var name = $"listed-{Guid.NewGuid()}";
            await admin.PostAsJsonAsync(BasePath, new { Name = name });

            var response = await admin.GetAsync(BasePath);
            var body = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().Contain(name);
            body.Should().NotContain("KeyHash", "the list response must never surface key material");
        }

        private Task<HttpResponseMessage> PostWebhookAsync(string apiKey)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, WebhookPath)
            {
                Content = JsonContent.Create(new
                {
                    ExternalAccountId = "never-linked",
                    Transactions = new[]
                    {
                        new { Id = "txn-1", Amount = -10.00m, Currency = "ZAR", Description = "test", Category = (string?)null, Date = DateTime.UtcNow }
                    }
                })
            };
            request.Headers.Add("X-Api-Key", apiKey);
            return _client.SendAsync(request);
        }
    }
}
