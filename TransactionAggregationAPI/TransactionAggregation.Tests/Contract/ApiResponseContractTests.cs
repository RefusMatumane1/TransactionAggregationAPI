using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Modules.Transactions.Infrastructure.Persistence;
using System.Net.Http.Json;
using System.Text.Json;
using TransactionAggregation.Tests.Helpers;
using TransactionAggregation.Tests.Integration;
using Xunit;

namespace TransactionAggregation.Tests.Contract
{
    public class ApiResponseContractTests : IClassFixture<IntegrationTestWebAppFactory>
    {
        private readonly IntegrationTestWebAppFactory _factory;

        public ApiResponseContractTests(IntegrationTestWebAppFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task NotFoundResponse_MatchesProblemDetailsContractIncludingTraceId()
        {
            using var client = _factory.CreateClient().SignedInAs("staff");

            var response = await client.GetAsync($"/api/v1/transactions/{Guid.NewGuid()}");

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.TryGetProperty("title", out _).Should().BeTrue();
            json.TryGetProperty("status", out var status).Should().BeTrue();
            status.GetInt32().Should().Be(404);
            json.TryGetProperty("type", out _).Should().BeTrue();
            json.TryGetProperty("traceId", out _).Should().BeTrue(
                "every error response must carry a traceId so operators can correlate it with logs (ADR/threat-model section 2)");
        }

        [Fact]
        public async Task ValidationFailureResponse_Returns400WithDetailAndTraceId()
        {
            using var client = _factory.CreateClient().SignedInAs("staff");

            var response = await client.GetAsync("/api/v1/transactions/aggregates/categories?from=2026-02-01&to=2026-01-01");

            response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.TryGetProperty("detail", out var detail).Should().BeTrue();
            detail.GetString().Should().NotBeNullOrEmpty();
            json.TryGetProperty("traceId", out _).Should().BeTrue();

            json.TryGetProperty("errors", out var errors).Should().BeTrue("validation failures are reported per field");
            errors.TryGetProperty("from", out var fromErrors).Should().BeTrue();
            fromErrors.GetArrayLength().Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task TransactionListItem_ContractIncludesAllFieldsApiConsumersDependOn()
        {
            var account = $"contract-{Guid.NewGuid():N}";
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
                context.Transactions.Add(TestTransactions.Create(-42m, "Contract test", account: account));
                await context.SaveChangesAsync();
            }
            using var client = _factory.CreateClient().SignedInAsStaffFor(TestInstitutions.FNB);

            var json = await client.GetFromJsonAsync<JsonElement>($"/api/v1/transactions?institution={TestInstitutions.FNB}&externalAccountId={account}");

            var item = json.GetProperty("items").EnumerateArray().Single();
            string[] expectedFields =
            [
                "id", "externalAccountId", "amount", "currency", "description",
                "category", "source", "date"
            ];
            foreach (var field in expectedFields)
                item.TryGetProperty(field, out _).Should().BeTrue($"TransactionListItemResponse must keep exposing '{field}' — the UI depends on it");
        }

        [Fact]
        public void ExternalTransactionDto_AcceptsTheProviderPayloadShapeProvidersActuallySend()
        {
            const string providerPayload = """
                {
                  "Id": "ext-txn-123",
                  "Amount": -49.99,
                  "Currency": "ZAR",
                  "Description": "Uber Trip",
                  "Category": null,
                  "Date": "2026-01-15T10:30:00Z"
                }
                """;

            var dto = JsonSerializer.Deserialize<Modules.Transactions.Application.Common.DTOs.ExternalTransactionDTO>(
                providerPayload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            dto.Should().NotBeNull();
            dto!.Id.Should().Be("ext-txn-123");
            dto.Amount.Should().Be(-49.99m);
            dto.Currency.Should().Be("ZAR");
            dto.Description.Should().Be("Uber Trip");
        }
    }
}