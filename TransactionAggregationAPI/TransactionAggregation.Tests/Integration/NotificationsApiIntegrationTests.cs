using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Modules.Notifications.Application.Common;
using Modules.Notifications.Application.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    public class NotificationsApiIntegrationTests : IClassFixture<IntegrationTestWebAppFactory>
    {
        private const string BanksPath = "/api/v1/admin/webhook-sources";
        private const string AlertsPath = "/api/v1/admin/notifications/duplicate-alert-webhooks";
        private const string AlertUrl = "https://hooks.example.com/services/T000/B000/ALERT-SECRET";

        private readonly IntegrationTestWebAppFactory _factory;

        public NotificationsApiIntegrationTests(IntegrationTestWebAppFactory factory) => _factory = factory;

        private HttpClient As(string? role)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, Guid.NewGuid().ToString());
            if (role is not null)
                client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeaderName, role);
            return client;
        }

        private static async Task<string> CreateBankAsync(HttpClient admin)
        {
            var code = $"alert-{Guid.NewGuid():N}";
            var response = await admin.PostAsJsonAsync(BanksPath, new { Code = code, DisplayName = code, Color = "#0033A1" });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            return code;
        }

        private async Task<(string? Stored, string? Plain)> StoredAsync(string bankCode)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<INotificationsDbContext>();
            var stored = context.AlertWebhooks.Where(w => w.BankCode == bankCode).Select(w => w.ProtectedUrl).SingleOrDefault();
            return (stored, stored is null ? null : scope.ServiceProvider.GetRequiredService<IAlertUrlProtector>().Unprotect(stored));
        }

        [Fact]
        public async Task Set_StoresItEncrypted_PerBank_AndNeverReturnsTheUrl()
        {
            using var admin = As("admin");
            var fnb = await CreateBankAsync(admin);
            var absa = await CreateBankAsync(admin);

            var set = await admin.PutAsJsonAsync($"{AlertsPath}/{fnb}", new { Url = AlertUrl });
            set.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var (stored, plain) = await StoredAsync(fnb);
            stored.Should().NotBeNull().And.NotContain("ALERT-SECRET", "the URL embeds a credential and is stored encrypted");
            plain.Should().Be(AlertUrl);
            (await StoredAsync(absa)).Stored.Should().BeNull("another bank's URL is not shared");

            var read = await admin.GetAsync($"{AlertsPath}/{fnb}");
            read.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await read.Content.ReadAsStringAsync();
            body.Should().NotContain("ALERT-SECRET").And.NotContain("hooks.example.com");
            var json = JsonDocument.Parse(body).RootElement;
            json.GetProperty("configured").GetBoolean().Should().BeTrue();
            json.GetProperty("bankCode").GetString().Should().Be(fnb);

            var other = JsonDocument.Parse(await admin.GetStringAsync($"{AlertsPath}/{absa}")).RootElement;
            other.GetProperty("configured").GetBoolean().Should().BeFalse();
        }

        [Fact]
        public async Task Set_IsCaseInsensitiveOnTheBankCode_AndStoredUnderTheRegisteredSpelling()
        {
            using var admin = As("admin");
            var bank = await CreateBankAsync(admin);

            (await admin.PutAsJsonAsync($"{AlertsPath}/{bank.ToUpperInvariant()}", new { Url = AlertUrl })).StatusCode.Should().Be(HttpStatusCode.NoContent);

            (await StoredAsync(bank)).Plain.Should().Be(AlertUrl);
        }

        [Fact]
        public async Task Set_AgainReplacesIt_AndABlankUrlClearsIt()
        {
            using var admin = As("admin");
            var bank = await CreateBankAsync(admin);
            await admin.PutAsJsonAsync($"{AlertsPath}/{bank}", new { Url = AlertUrl });

            await admin.PutAsJsonAsync($"{AlertsPath}/{bank}", new { Url = "https://other.example.com/hook" });
            (await StoredAsync(bank)).Plain.Should().Be("https://other.example.com/hook");

            var clear = await admin.PutAsJsonAsync($"{AlertsPath}/{bank}", new { Url = "" });

            clear.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await StoredAsync(bank)).Stored.Should().BeNull();
            JsonDocument.Parse(await admin.GetStringAsync($"{AlertsPath}/{bank}")).RootElement.GetProperty("configured").GetBoolean().Should().BeFalse();
        }

        [Theory]
        [InlineData("http://hooks.example.com/hook")]
        [InlineData("https://localhost/hook")]
        [InlineData("https://169.254.169.254/latest/meta-data")]
        [InlineData("https://10.0.0.5/hook")]
        [InlineData("https://user:pass@hooks.example.com/hook")]
        [InlineData("not a url")]
        public async Task Set_UnsafeOrMalformedUrl_Returns400_AndStoresNothing(string url)
        {
            using var admin = As("admin");
            var bank = await CreateBankAsync(admin);

            var response = await admin.PutAsJsonAsync($"{AlertsPath}/{bank}", new { Url = url });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("url", out _).Should().BeTrue();
            (await StoredAsync(bank)).Stored.Should().BeNull();
        }

        [Fact]
        public async Task UnknownBank_Returns404_OnBothReadAndWrite()
        {
            using var admin = As("admin");

            (await admin.PutAsJsonAsync($"{AlertsPath}/no-such-bank", new { Url = AlertUrl })).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await admin.GetAsync($"{AlertsPath}/no-such-bank")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Theory]
        [InlineData("staff")]
        [InlineData(null)]
        public async Task NonAdmins_Get403_OnBothReadAndWrite(string? role)
        {
            using var client = As(role);

            (await client.GetAsync($"{AlertsPath}/FNB")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await client.PutAsJsonAsync($"{AlertsPath}/FNB", new { Url = AlertUrl })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Anonymous_Gets401()
        {
            using var client = _factory.CreateClient();

            (await client.GetAsync($"{AlertsPath}/FNB")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            (await client.PutAsJsonAsync($"{AlertsPath}/FNB", new { Url = AlertUrl })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}