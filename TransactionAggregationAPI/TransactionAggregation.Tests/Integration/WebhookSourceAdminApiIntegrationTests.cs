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
        private const string BanksPath = "/api/v1/banks";
        private const string WebhookPath = "/api/v1/webhooks/bank-aggregator/transactions";

        private readonly IntegrationTestWebAppFactory _factory;
        private readonly HttpClient _client;

        public WebhookSourceAdminApiIntegrationTests(IntegrationTestWebAppFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private record CreateResponse(Guid Id, string Code, string DisplayName, string Color, string ApiKey);
        private record RotateResponse(string ApiKey);

        // Bank codes are letters, digits, '-' or '_' and at most 50 characters.
        private static string NewCode(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

        private static object Bank(string code, string? displayName = null, string color = "#0033A1") =>
            new { Code = code, DisplayName = displayName ?? code, Color = color };

        private HttpClient AsAdmin() => AsRole("admin");

        private HttpClient AsRole(string role)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, Guid.NewGuid().ToString());
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeaderName, role);
            return client;
        }

        private static async Task<CreateResponse> CreateAsync(HttpClient admin, string code, string? displayName = null, string color = "#0033A1")
        {
            var response = await admin.PostAsJsonAsync(BasePath, Bank(code, displayName, color));
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await response.Content.ReadFromJsonAsync<CreateResponse>())!;
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

            var created = await CreateAsync(admin, NewCode("lifecycle"));
            created.ApiKey.Should().NotBeNullOrWhiteSpace();

            (await PostWebhookAsync(created.ApiKey)).StatusCode.Should().Be(HttpStatusCode.Accepted);

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
        public async Task Webhook_NamingAnotherBankThanTheKeys_Returns400_AndNamingItsOwnIsAccepted()
        {
            using var admin = AsAdmin();
            var code = NewCode("own");
            var created = await CreateAsync(admin, code);

            var other = await PostWebhookAsync(created.ApiKey, institution: "Absa");
            other.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await other.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
                .TryGetProperty("institution", out _).Should().BeTrue("the refusal names the offending field");

            (await PostWebhookAsync(created.ApiKey, institution: code.ToUpperInvariant())).StatusCode
                .Should().Be(HttpStatusCode.Accepted, "a bank naming itself is fine, in any case");
        }

        [Fact]
        public async Task CreateSource_DuplicateCode_Returns409_EvenInAnotherCase()
        {
            using var admin = AsAdmin();
            var code = NewCode("dup");

            await CreateAsync(admin, code);
            var response = await admin.PostAsJsonAsync(BasePath, Bank(code.ToUpperInvariant()));

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
        public async Task GetSources_ListsCreatedBankWithoutKeyMaterial()
        {
            using var admin = AsAdmin();
            var code = NewCode("listed");
            var created = await CreateAsync(admin, code, "Listed Bank", "#dc0032");

            var listed = (await ListAllSourcesAsync(admin)).Single(s => s.GetProperty("id").GetGuid() == created.Id);
            listed.GetProperty("code").GetString().Should().Be(code);
            listed.GetProperty("displayName").GetString().Should().Be("Listed Bank");
            listed.GetProperty("color").GetString().Should().Be("#DC0032", "colours are stored upper-case");
            listed.TryGetProperty("keyHash", out _).Should().BeFalse("the list response must never surface key material");
        }

        [Theory]
        [InlineData("has space", "Bank", "#0033A1", "code")]
        [InlineData("dotted.code", "Bank", "#0033A1", "code")]
        [InlineData("Valid", "", "#0033A1", "displayName")]
        [InlineData("Valid", "Bank", "blue", "color")]
        public async Task CreateSource_InvalidField_Returns400WithFieldLevelErrors(string code, string displayName, string color, string field)
        {
            using var admin = AsAdmin();

            var response = await admin.PostAsJsonAsync(BasePath, new { Code = code, DisplayName = displayName, Color = color });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            problem.GetProperty("errors").TryGetProperty(field, out var messages)
                .Should().BeTrue("validation errors are reported per field, keyed by the JSON property name");
            messages.GetArrayLength().Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task UpdateSource_ChangesDisplayNameAndColour_ButNotTheCode()
        {
            using var admin = AsAdmin();
            var code = NewCode("renamed");
            var created = await CreateAsync(admin, code);

            var update = await admin.PutAsJsonAsync($"{BasePath}/{created.Id}", new { DisplayName = "Renamed Bank", Color = "#1c3a70" });
            update.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var listed = (await ListAllSourcesAsync(admin)).Single(s => s.GetProperty("id").GetGuid() == created.Id);
            listed.GetProperty("code").GetString().Should().Be(code);
            listed.GetProperty("displayName").GetString().Should().Be("Renamed Bank");
            listed.GetProperty("color").GetString().Should().Be("#1C3A70");
        }

        [Fact]
        public async Task UpdateSource_InvalidColour_Returns400_NotA500()
        {
            using var admin = AsAdmin();

            var response = await admin.PutAsJsonAsync($"{BasePath}/{Guid.NewGuid()}", new { DisplayName = "Bank", Color = "red" });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
                "a non-generic Result command failing validation must map to 400, not throw into the 500 handler");
        }

        [Fact]
        public async Task UpdateUnknownSource_Returns404()
        {
            using var admin = AsAdmin();

            var response = await admin.PutAsJsonAsync($"{BasePath}/{Guid.NewGuid()}", new { DisplayName = "Bank", Color = "#0033A1" });

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Banks_AreReadableByStaff_WithTheirLookAndState_ButNoKeys()
        {
            using var admin = AsAdmin();
            var code = NewCode("staffview");
            var created = await CreateAsync(admin, code, "Staff View Bank", "#00A3AD");
            await admin.PostAsync($"{BasePath}/{created.Id}/deactivate", null);
            using var staff = AsRole("staff");

            var response = await staff.GetAsync(BanksPath);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var bank = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()
                .Single(b => b.GetProperty("code").GetString() == code);
            bank.GetProperty("displayName").GetString().Should().Be("Staff View Bank");
            bank.GetProperty("color").GetString().Should().Be("#00A3AD");
            bank.GetProperty("isActive").GetBoolean().Should().BeFalse("inactive banks stay listed so their history still has a name");
            bank.TryGetProperty("id", out _).Should().BeFalse("staff get the look of a bank, not handles to manage it");
        }

        [Fact]
        public async Task Banks_Anonymous_Returns401() =>
            (await _client.GetAsync(BanksPath)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        [Fact]
        public async Task RegisterSigningKey_P256Key_IsAcceptedAndListed()
        {
            using var admin = AsAdmin();
            using var key = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
            var created = await CreateAsync(admin, NewCode("signed"));

            var register = await admin.PutAsJsonAsync($"{BasePath}/{created.Id}/signing-key",
                new { PublicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()) });
            register.StatusCode.Should().Be(HttpStatusCode.NoContent);

            (await ListAllSourcesAsync(admin)).Single(s => s.GetProperty("id").GetGuid() == created.Id)
                .GetProperty("signingKeyRegistered").GetBoolean().Should().BeTrue();
        }

        [Fact]
        public async Task RegisterSigningKey_NotAP256Key_Returns400WithFieldLevelErrors()
        {
            using var admin = AsAdmin();
            using var rsa = System.Security.Cryptography.RSA.Create(2048);

            var response = await admin.PutAsJsonAsync($"{BasePath}/{Guid.NewGuid()}/signing-key",
                new { PublicKey = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo()) });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors")
                .TryGetProperty("publicKey", out var messages).Should().BeTrue();
            messages.GetArrayLength().Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task RegisterSigningKey_AuthenticatedNonAdmin_Returns403()
        {
            using var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, Guid.NewGuid().ToString());

            var response = await client.PutAsJsonAsync($"{BasePath}/{Guid.NewGuid()}/signing-key", new { PublicKey = "x" });

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        // Other tests add sources to the same host, so a given source may be on any page.
        private static async Task<List<JsonElement>> ListAllSourcesAsync(HttpClient admin)
        {
            var sources = new List<JsonElement>();
            string? cursor = null;
            do
            {
                var page = await admin.GetFromJsonAsync<JsonElement>(
                    cursor is null ? $"{BasePath}?pageSize=2" : $"{BasePath}?pageSize=2&cursor={Uri.EscapeDataString(cursor)}");
                sources.AddRange(page.GetProperty("items").EnumerateArray());
                cursor = page.GetProperty("hasMore").GetBoolean() ? page.GetProperty("nextCursor").GetString() : null;
            }
            while (cursor is not null);
            return sources;
        }

        [Fact]
        public async Task GetSources_InvalidCursor_Returns400()
        {
            using var admin = AsAdmin();

            var response = await admin.GetAsync($"{BasePath}?cursor=not-a-cursor");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        private Task<HttpResponseMessage> PostWebhookAsync(string apiKey, string? institution = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, WebhookPath)
            {
                Content = JsonContent.Create(new
                {
                    ExternalAccountId = "never-linked",
                    Institution = institution,
                    Transactions = new[]
                    {
                        new { Id = $"txn-{Guid.NewGuid():N}", Amount = -10.00m, Currency = "ZAR", Description = "test", Category = (string?)null, Date = DateTime.UtcNow }
                    }
                })
            };
            request.Headers.Add("X-Api-Key", apiKey);
            return _client.SendAsync(request);
        }
    }
}