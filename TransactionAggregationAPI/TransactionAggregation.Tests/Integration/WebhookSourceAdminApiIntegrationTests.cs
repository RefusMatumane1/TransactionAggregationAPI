using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

        private static readonly string[] Institutions = ["FNB", "Absa"];

        private record CreateResponse(Guid Id, string Name, string ApiKey, string[] AuthorizedInstitutions);
        private record RotateResponse(string ApiKey);

        private HttpClient AsAdmin()
        {

            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeaderName, "admin");
            return client;
        }

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

        [Fact]
        public async Task CreateRotateDeactivate_FullLifecycle_Works()
        {
            using var admin = AsAdmin();

            var createResponse = await admin.PostAsJsonAsync(BasePath, new { Name = $"lifecycle-{Guid.NewGuid()}", AuthorizedInstitutions = Institutions });
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var created = await createResponse.Content.ReadFromJsonAsync<CreateResponse>();
            created.Should().NotBeNull();
            created!.ApiKey.Should().NotBeNullOrWhiteSpace();

            var firstWebhookCall = await PostWebhookAsync(created.ApiKey);
            firstWebhookCall.StatusCode.Should().Be(HttpStatusCode.Accepted);

            var rotateResponse = await admin.PostAsync($"{BasePath}/{created.Id}/rotate", null);
            rotateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var rotated = await rotateResponse.Content.ReadFromJsonAsync<RotateResponse>();
            rotated!.ApiKey.Should().NotBe(created.ApiKey);

            (await PostWebhookAsync(created.ApiKey)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await PostWebhookAsync(rotated.ApiKey)).StatusCode.Should().Be(HttpStatusCode.Accepted);

            var deactivateResponse = await admin.PostAsync($"{BasePath}/{created.Id}/deactivate", null);
            deactivateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            (await PostWebhookAsync(rotated.ApiKey)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var activateResponse = await admin.PostAsync($"{BasePath}/{created.Id}/activate", null);
            activateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            (await PostWebhookAsync(rotated.ApiKey)).StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        [Fact]
        public async Task CreateSource_DuplicateName_Returns409()
        {
            using var admin = AsAdmin();
            var name = $"dup-{Guid.NewGuid()}";

            await admin.PostAsJsonAsync(BasePath, new { Name = name, AuthorizedInstitutions = Institutions });
            var response = await admin.PostAsJsonAsync(BasePath, new { Name = name, AuthorizedInstitutions = Institutions });

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
            await admin.PostAsJsonAsync(BasePath, new { Name = name, AuthorizedInstitutions = Institutions });

            var response = await admin.GetAsync(BasePath);
            var body = await response.Content.ReadAsStringAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().Contain(name);
            body.Should().NotContain("KeyHash", "the list response must never surface key material");
        }

        [Fact]
        public async Task CreateSource_WithoutAuthorizedInstitutions_Returns400WithFieldLevelErrors()
        {
            using var admin = AsAdmin();

            var response = await admin.PostAsJsonAsync(BasePath, new { Name = $"unscoped-{Guid.NewGuid()}" });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            problem.GetProperty("errors").TryGetProperty("authorizedInstitutions", out var messages)
                .Should().BeTrue("validation errors are reported per field, keyed by the JSON property name");
            messages.GetArrayLength().Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task UpdateInstitutions_ReplacesTheScope_AndIsListed()
        {
            using var admin = AsAdmin();
            var created = await (await admin.PostAsJsonAsync(BasePath,
                    new { Name = $"rescoped-{Guid.NewGuid()}", AuthorizedInstitutions = Institutions }))
                .Content.ReadFromJsonAsync<CreateResponse>();
            created!.AuthorizedInstitutions.Should().BeEquivalentTo(Institutions);

            var update = await admin.PutAsJsonAsync($"{BasePath}/{created.Id}/institutions",
                new { AuthorizedInstitutions = new[] { "Capitec", "capitec", " StandardBank " } });
            update.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var listed = await admin.GetFromJsonAsync<JsonElement>(BasePath);
            var source = listed.EnumerateArray().Single(s => s.GetProperty("id").GetGuid() == created.Id);
            source.GetProperty("authorizedInstitutions").EnumerateArray().Select(i => i.GetString())
                .Should().BeEquivalentTo(["Capitec", "StandardBank"], "names are trimmed and de-duplicated case-insensitively");
        }

        [Fact]
        public async Task UpdateInstitutions_EmptyList_Returns400_NotA500()
        {
            using var admin = AsAdmin();

            var response = await admin.PutAsJsonAsync($"{BasePath}/{Guid.NewGuid()}/institutions",
                new { AuthorizedInstitutions = Array.Empty<string>() });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "a non-generic Result command failing validation must map to 400, not throw into the 500 handler");
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