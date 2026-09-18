using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using TransactionAggregation.Domain.Enums;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    /// <summary>
    /// IDOR/BOLA regression tests for the account endpoints (threat model, section 2):
    /// a caller must never be able to read or mutate another customer's accounts by
    /// editing an ID in the URL, whether that ID is the customerId segment or the
    /// accountId segment.
    /// </summary>
    public class AccountApiSecurityTests : IClassFixture<IntegrationTestWebAppFactory>
    {
        private readonly IntegrationTestWebAppFactory _factory;

        public AccountApiSecurityTests(IntegrationTestWebAppFactory factory)
        {
            _factory = factory;
        }

        private record CreateAccountRequestBody(string AccountNumber, string AccountName, AccountType AccountType, string Currency = "ZAR");

        private HttpClient AsCustomer(Guid customerId)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, customerId.ToString());
            return client;
        }

        private async Task<Guid> CreateCustomerAsync(string email)
        {
            using var anonymous = _factory.CreateClient();
            var response = await anonymous.PostAsJsonAsync(
                "/api/v1/customers", new { Email = email, Name = "Test User", Password = "Password1" });
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Guid>();
        }

        private static async Task<Guid> CreateAccountAsync(HttpClient asOwner, Guid ownerCustomerId)
        {
            var response = await asOwner.PostAsJsonAsync(
                $"/api/v1/customers/{ownerCustomerId}/accounts",
                new CreateAccountRequestBody($"acc-{Guid.NewGuid():N}", "Everyday Account", AccountType.Checking));
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Guid>();
        }

        [Fact]
        public async Task GetAccountById_UnderAnotherCustomersUrlSegment_Returns404NotTheAccount()
        {
            var ownerId = await CreateCustomerAsync($"{Guid.NewGuid()}@example.com");
            var attackerId = await CreateCustomerAsync($"{Guid.NewGuid()}@example.com");
            using var owner = AsCustomer(ownerId);
            var accountId = await CreateAccountAsync(owner, ownerId);

            using var attacker = AsCustomer(attackerId);
            var response = await attacker.GetAsync($"/api/v1/customers/{attackerId}/accounts/{accountId}");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetAccountById_OwnCustomerIdButAnotherCustomersAccountId_Returns404()
        {
            // The specific IDOR case: the attacker puts their OWN customerId in the URL
            // (passing the first ownership check) but a guessed/enumerated accountId that
            // belongs to someone else. The handler must still refuse.
            var ownerId = await CreateCustomerAsync($"{Guid.NewGuid()}@example.com");
            var attackerId = await CreateCustomerAsync($"{Guid.NewGuid()}@example.com");
            using var owner = AsCustomer(ownerId);
            var victimAccountId = await CreateAccountAsync(owner, ownerId);

            using var attacker = AsCustomer(attackerId);
            var response = await attacker.GetAsync($"/api/v1/customers/{attackerId}/accounts/{victimAccountId}");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetCustomerAccounts_ForAnotherCustomer_Returns404()
        {
            var ownerId = await CreateCustomerAsync($"{Guid.NewGuid()}@example.com");
            var attackerId = await CreateCustomerAsync($"{Guid.NewGuid()}@example.com");
            using var owner = AsCustomer(ownerId);
            await CreateAccountAsync(owner, ownerId);

            using var attacker = AsCustomer(attackerId);
            var response = await attacker.GetAsync($"/api/v1/customers/{ownerId}/accounts");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CreateAccount_UnderAnotherCustomersUrlSegment_Returns404()
        {
            var ownerId = await CreateCustomerAsync($"{Guid.NewGuid()}@example.com");
            var attackerId = await CreateCustomerAsync($"{Guid.NewGuid()}@example.com");

            using var attacker = AsCustomer(attackerId);
            var response = await attacker.PostAsJsonAsync(
                $"/api/v1/customers/{ownerId}/accounts",
                new CreateAccountRequestBody("acc-injected", "Injected Account", AccountType.Checking));

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task DeactivateAccount_BelongingToAnotherCustomer_Returns404AndLeavesItActive()
        {
            var ownerId = await CreateCustomerAsync($"{Guid.NewGuid()}@example.com");
            var attackerId = await CreateCustomerAsync($"{Guid.NewGuid()}@example.com");
            using var owner = AsCustomer(ownerId);
            var victimAccountId = await CreateAccountAsync(owner, ownerId);

            using var attacker = AsCustomer(attackerId);
            var deactivateResponse = await attacker.PatchAsync(
                $"/api/v1/customers/{attackerId}/accounts/{victimAccountId}/deactivate", content: null);

            deactivateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var verifyResponse = await owner.GetAsync($"/api/v1/customers/{ownerId}/accounts/{victimAccountId}");
            var body = await verifyResponse.Content.ReadAsStringAsync();
            body.Should().Contain("\"isActive\":true");
        }

        [Fact]
        public async Task GetCustomerAccounts_Unauthenticated_Returns401()
        {
            var ownerId = await CreateCustomerAsync($"{Guid.NewGuid()}@example.com");
            using var anonymous = _factory.CreateClient();

            var response = await anonymous.GetAsync($"/api/v1/customers/{ownerId}/accounts");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
