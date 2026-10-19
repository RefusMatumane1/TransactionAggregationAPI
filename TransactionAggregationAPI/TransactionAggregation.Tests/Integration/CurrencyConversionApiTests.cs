using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Modules.Reporting.Infrastructure.Persistence;
using Modules.Transactions.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    public class CurrencyConversionApiTests(IntegrationTestWebAppFactory factory) : IClassFixture<IntegrationTestWebAppFactory>
    {
        private async Task<(string Account, Guid UsdId)> SeedAsync()
        {
            var account = $"fx-{Guid.NewGuid():N}";
            var usd = TestTransactions.Create(-10m, "Online store", account: account, currency: "USD", date: DateTime.UtcNow.AddMinutes(-1));
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
            context.Transactions.AddRange(
                TestTransactions.Create(-100m, "Local shop", account: account, date: DateTime.UtcNow.AddMinutes(-2)),
                usd,
                TestTransactions.Create(-4m, "Dinner", account: account, currency: "EUR", date: DateTime.UtcNow.AddMinutes(-3)));
            await context.SaveChangesAsync();
            await InMemoryDailyTotals.BuildAsync(context, scope.ServiceProvider.GetRequiredService<ReportingDbContext>());
            return (account, usd.Id.Value);
        }

        private HttpClient Admin() => factory.CreateClient().SignedInAs("admin");

        private static string Scope(string account) => $"institution={TestInstitutions.FNB}&externalAccountId={account}";

        [Fact]
        public async Task Aggregates_ConvertedWithConvertTo_AreInTheTargetAndSayHowTheyWereBuilt()
        {
            var (account, _) = await SeedAsync();
            using var admin = Admin();

            var json = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/transactions/aggregates/cash-flow?{Scope(account)}&convertTo=ZAR");

            json.GetProperty("currency").GetString().Should().Be("ZAR");
            json.GetProperty("totalExpenses").GetDecimal().Should().Be(100m + 10m * 18m + 4m * 20.5m);
            var conversion = json.GetProperty("conversion");
            conversion.GetProperty("target").GetString().Should().Be("ZAR");
            conversion.GetProperty("ratesVersion").GetString().Should().HaveLength(12);
            conversion.GetProperty("rates").EnumerateArray().Select(r => r.GetProperty("currency").GetString())
                .Should().Equal("EUR", "USD", "ZAR");
            conversion.GetProperty("unconvertedCurrencies").GetArrayLength().Should().Be(0);
        }

        [Fact]
        public async Task Currencies_ListsTheBaseAndEverySupportedCurrency_WithItsRateToTheBase()
        {
            using var staff = factory.CreateClient().SignedInAs("staff");

            var json = await staff.GetFromJsonAsync<JsonElement>("/api/v1/currencies");

            json.GetProperty("baseCurrency").GetString().Should().Be("ZAR");
            json.GetProperty("ratesVersion").GetString().Should().HaveLength(12);
            var rates = json.GetProperty("currencies").EnumerateArray()
                .ToDictionary(c => c.GetProperty("code").GetString()!, c => c.GetProperty("rateToBase").GetDecimal());
            rates["ZAR"].Should().Be(1m);
            rates["USD"].Should().Be(18m);
            rates.Keys.Should().BeInAscendingOrder(StringComparer.Ordinal);
        }

        [Fact]
        public async Task Currencies_RequiresSignIn()
        {
            using var anonymous = factory.CreateClient();

            (await anonymous.GetAsync("/api/v1/currencies")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task EveryCurrencyTheEndpointListsIsAcceptedByConvertTo()
        {
            var (account, _) = await SeedAsync();
            using var admin = Admin();
            var listed = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/currencies")).GetProperty("currencies")
                .EnumerateArray().Select(c => c.GetProperty("code").GetString()!).ToList();

            foreach (var code in listed)
                (await admin.GetAsync($"/api/v1/transactions/aggregates/cash-flow?{Scope(account)}&convertTo={code}"))
                    .StatusCode.Should().Be(HttpStatusCode.OK, $"{code} is on the list");
        }

        [Fact]
        public async Task Aggregates_WithoutConvertTo_AreAsBefore()
        {
            var (account, _) = await SeedAsync();
            using var admin = Admin();

            var zar = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/transactions/aggregates/cash-flow?{Scope(account)}");
            var usd = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/transactions/aggregates/cash-flow?{Scope(account)}&currency=USD");

            zar.GetProperty("totalExpenses").GetDecimal().Should().Be(100m);
            zar.TryGetProperty("conversion", out var conversion).Should().BeTrue();
            conversion.ValueKind.Should().Be(JsonValueKind.Null);
            usd.GetProperty("totalExpenses").GetDecimal().Should().Be(10m);
        }

        [Theory]
        [InlineData("cash-flow")]
        [InlineData("categories")]
        [InlineData("institutions")]
        [InlineData("comparison")]
        public async Task EveryAggregate_AcceptsConvertTo(string view)
        {
            var (account, _) = await SeedAsync();
            using var admin = Admin();

            var response = await admin.GetAsync($"/api/v1/transactions/aggregates/{view}?{Scope(account)}&convertTo=USD");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("conversion").GetProperty("target").GetString().Should().Be("USD");
        }

        [Fact]
        public async Task Summary_AcceptsConvertTo()
        {
            var (account, _) = await SeedAsync();
            using var admin = Admin();

            var json = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/transactions/summary?{Scope(account)}&convertTo=ZAR");

            json.GetProperty("totalExpenses").GetDecimal().Should().Be(100m + 180m + 82m);
            json.GetProperty("conversion").GetProperty("target").GetString().Should().Be("ZAR");
        }

        [Theory]
        [InlineData("zar", "convertTo")]
        [InlineData("ZA", "convertTo")]
        [InlineData("XAF", null)]
        [InlineData("ZZZ", null)]
        public async Task ConvertTo_ACurrencyThatIsNotUpperCaseOrNotConfigured_Returns400(string target, string? field)
        {
            var (account, _) = await SeedAsync();
            using var admin = Admin();

            var response = await admin.GetAsync($"/api/v1/transactions/aggregates/cash-flow?{Scope(account)}&convertTo={target}");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var body = await response.Content.ReadAsStringAsync();
            if (field is not null)
                JsonDocument.Parse(body).RootElement.GetProperty("errors").TryGetProperty(field, out _).Should().BeTrue();
            else
                body.Should().Contain("supported currencies");
        }

        [Fact]
        public async Task TheList_AddsConvertedAmounts_OnlyWhenAsked()
        {
            var (account, _) = await SeedAsync();
            using var admin = Admin();

            var plain = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/transactions?{Scope(account)}");
            var converted = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/transactions?{Scope(account)}&convertTo=ZAR");

            plain.GetProperty("items").EnumerateArray().Should().OnlyContain(i => i.GetProperty("convertedAmount").ValueKind == JsonValueKind.Null);
            var usd = converted.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("currency").GetString() == "USD");
            usd.GetProperty("amount").GetDecimal().Should().Be(-10m);
            usd.GetProperty("convertedAmount").GetDecimal().Should().Be(-180m);
            usd.GetProperty("convertedCurrency").GetString().Should().Be("ZAR");
        }

        [Fact]
        public async Task TheDetail_AddsTheConvertedAmount_AndTheCustomerViewsAcceptConvertTo()
        {
            var (_, usdId) = await SeedAsync();
            using var admin = Admin();

            var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/transactions/{usdId}?convertTo=EUR");

            detail.GetProperty("convertedAmount").GetDecimal().Should().Be(-8.7805m, "10 USD * 18 / 20.5 = 8.78049 -> 8.7805");
            detail.GetProperty("convertedCurrency").GetString().Should().Be("EUR");
            (await admin.GetAsync($"/api/v1/transactions/{usdId}?convertTo=XAF")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}