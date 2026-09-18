using FluentAssertions;
using System.Net.Http.Json;
using System.Text.Json;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Tests.Integration;
using Xunit;

namespace TransactionAggregation.Tests.Contract
{
    /// <summary>
    /// Locks down the JSON shape API consumers depend on (Instructions.md section 31,
    /// "contract tests... between API consumers"). These are deliberately shallow —
    /// property-name/shape checks, not business-logic assertions (those live in the
    /// Unit/Integration suites) — so a rename or dropped field fails CI instead of
    /// breaking an external consumer silently.
    /// </summary>
    public class ApiResponseContractTests : IClassFixture<IntegrationTestWebAppFactory>
    {
        private readonly IntegrationTestWebAppFactory _factory;

        public ApiResponseContractTests(IntegrationTestWebAppFactory factory)
        {
            _factory = factory;
        }

        private record CreateAccountRequestBody(string AccountNumber, string AccountName, AccountType AccountType, string Currency = "ZAR");

        [Fact]
        public async Task NotFoundResponse_MatchesProblemDetailsContractIncludingTraceId()
        {
            using var client = _factory.CreateClient();
            var customerId = Guid.NewGuid();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, customerId.ToString());

            var response = await client.GetAsync($"/api/v1/customers/{customerId}");

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            // RFC 7807 ProblemDetails contract, plus the traceId extension every error
            // response must carry so a client-visible failure can be correlated to logs.
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
            using var anonymous = _factory.CreateClient();

            // Missing required Email/Name/password triggers FluentValidation.
            // NOTE: ValidationBehavior (Common/Behaviors/ValidationBehavior.cs) currently
            // joins all failures into a single Error.Validation string rather than the
            // structured ValidationError type (which exposes a per-field "errors" array,
            // see CustomResults.GetExtensions) — that structured type exists but isn't
            // wired into this pipeline. This test pins today's actual contract; if that
            // pipeline is later changed to return structured per-field errors, this
            // assertion should be updated to check for the "errors" property instead.
            var response = await anonymous.PostAsJsonAsync("/api/v1/customers", new { });

            response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            json.TryGetProperty("detail", out var detail).Should().BeTrue();
            detail.GetString().Should().NotBeNullOrEmpty();
            json.TryGetProperty("traceId", out _).Should().BeTrue();
        }

        [Fact]
        public async Task AccountResponse_ContractIncludesAllFieldsApiConsumersDependOn()
        {
            using var client = _factory.CreateClient();
            var createCustomerResponse = await client.PostAsJsonAsync(
                "/api/v1/customers",
                new { Email = $"{Guid.NewGuid()}@example.com", Name = "Contract Test User", Password = "Password1" });
            createCustomerResponse.EnsureSuccessStatusCode();
            var customerId = await createCustomerResponse.Content.ReadFromJsonAsync<Guid>();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, customerId.ToString());

            var createAccountResponse = await client.PostAsJsonAsync(
                $"/api/v1/customers/{customerId}/accounts",
                new CreateAccountRequestBody($"acc-{Guid.NewGuid():N}", "Contract Account", AccountType.Checking));
            createAccountResponse.EnsureSuccessStatusCode();
            var accountId = await createAccountResponse.Content.ReadFromJsonAsync<Guid>();

            var getResponse = await client.GetAsync($"/api/v1/customers/{customerId}/accounts/{accountId}");
            var json = await getResponse.Content.ReadFromJsonAsync<JsonElement>();

            string[] expectedFields =
            [
                "id", "customerId", "accountNumber", "accountName",
                "accountType", "balance", "currency", "isActive", "createdAt"
            ];
            foreach (var field in expectedFields)
                json.TryGetProperty(field, out _).Should().BeTrue($"AccountResponse must keep exposing '{field}' — API consumers depend on it");
        }

        [Fact]
        public void ExternalTransactionDto_AcceptsTheProviderPayloadShapeProvidersActuallySend()
        {
            // Pins the inbound provider contract (Instructions.md section 31, "contracts
            // between provider events"): if a provider integration relies on these exact
            // property names/casing, a rename here must be a deliberate, visible change.
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

            var dto = JsonSerializer.Deserialize<TransactionAggregation.Application.Common.DTOs.ExternalTransactionDTO>(
                providerPayload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            dto.Should().NotBeNull();
            dto!.Id.Should().Be("ext-txn-123");
            dto.Amount.Should().Be(-49.99m);
            dto.Currency.Should().Be("ZAR");
            dto.Description.Should().Be("Uber Trip");
        }
    }
}
