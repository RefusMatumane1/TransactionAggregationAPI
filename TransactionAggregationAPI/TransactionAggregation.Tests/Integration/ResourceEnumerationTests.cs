using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Modules.Customers.Domain.ValueObjects;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using Modules.Transactions.Infrastructure.Persistence;
using SharedKernel.Common.ValueObjects;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    /// <summary>
    /// Enumeration regression tests (threat model, section 2): a resource that exists but
    /// belongs to another customer must be indistinguishable from one that doesn't exist.
    /// If the two 404s differed at all (empty body vs ProblemDetails, different title), any
    /// caller could probe ids or email addresses and learn which ones are real. Each test
    /// compares the two responses field by field, ignoring only the per-request traceId and
    /// the key that was looked up.
    /// </summary>
    public class ResourceEnumerationTests : IClassFixture<IntegrationTestWebAppFactory>
    {
        private readonly IntegrationTestWebAppFactory _factory;

        public ResourceEnumerationTests(IntegrationTestWebAppFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GetCustomerByEmail_AnotherCustomersEmail_IsIndistinguishableFromUnregisteredEmail()
        {
            var victimEmail = $"{Guid.NewGuid()}@example.com";
            await CreateCustomerAsync(victimEmail);
            var attackerId = await CreateCustomerAsync($"{Guid.NewGuid()}@example.com");
            var unregisteredEmail = $"{Guid.NewGuid()}@example.com";
            using var attacker = AsCustomer(attackerId);

            var notOwned = await attacker.GetAsync($"/api/v1/customers/email/{Uri.EscapeDataString(victimEmail)}");
            var missing = await attacker.GetAsync($"/api/v1/customers/email/{Uri.EscapeDataString(unregisteredEmail)}");

            await AssertIndistinguishableAsync(notOwned, victimEmail, missing, unregisteredEmail);
        }

        [Fact]
        public async Task GetAccountById_AnotherCustomersAccount_IsIndistinguishableFromMissingAccount()
        {
            var (attacker, attackerId, victimAccountId) = await AttackerAndVictimAccountAsync();
            var missingAccountId = Guid.NewGuid();

            var notOwned = await attacker.GetAsync($"/api/v1/customers/{attackerId}/accounts/{victimAccountId}");
            var missing = await attacker.GetAsync($"/api/v1/customers/{attackerId}/accounts/{missingAccountId}");

            await AssertIndistinguishableAsync(notOwned, victimAccountId.ToString(), missing, missingAccountId.ToString());
        }

        [Fact]
        public async Task DeactivateAccount_AnotherCustomersAccount_IsIndistinguishableFromMissingAccount()
        {
            var (attacker, attackerId, victimAccountId) = await AttackerAndVictimAccountAsync();
            var missingAccountId = Guid.NewGuid();

            var notOwned = await attacker.PatchAsync($"/api/v1/customers/{attackerId}/accounts/{victimAccountId}/deactivate", null);
            var missing = await attacker.PatchAsync($"/api/v1/customers/{attackerId}/accounts/{missingAccountId}/deactivate", null);

            await AssertIndistinguishableAsync(notOwned, victimAccountId.ToString(), missing, missingAccountId.ToString());
        }

        [Fact]
        public async Task GetTransactionById_AnotherCustomersTransaction_IsIndistinguishableFromMissingTransaction()
        {
            var victimTransactionId = await SeedTransactionAsync(customerId: Guid.NewGuid());
            using var attacker = AsCustomer(await CreateCustomerAsync($"{Guid.NewGuid()}@example.com"));
            var missingTransactionId = Guid.NewGuid();

            var notOwned = await attacker.GetAsync($"/api/v1/transactions/{victimTransactionId}");
            var missing = await attacker.GetAsync($"/api/v1/transactions/{missingTransactionId}");

            await AssertIndistinguishableAsync(notOwned, victimTransactionId.ToString(), missing, missingTransactionId.ToString());
        }

        [Fact]
        public async Task CategorizeTransaction_AnotherCustomersTransaction_IsIndistinguishableFromMissingTransaction()
        {
            var victimTransactionId = await SeedTransactionAsync(customerId: Guid.NewGuid());
            using var attacker = AsCustomer(await CreateCustomerAsync($"{Guid.NewGuid()}@example.com"));
            var missingTransactionId = Guid.NewGuid();
            var body = new { Category = TransactionCategory.Groceries };

            var notOwned = await attacker.PatchAsJsonAsync($"/api/v1/transactions/{victimTransactionId}/categorize", body);
            var missing = await attacker.PatchAsJsonAsync($"/api/v1/transactions/{missingTransactionId}/categorize", body);

            await AssertIndistinguishableAsync(notOwned, victimTransactionId.ToString(), missing, missingTransactionId.ToString());
        }

        [Fact]
        public async Task CustomerRoute_AnotherCustomersId_MatchesTheMissingCustomerResponse()
        {
            // The route filter refuses another customer's id without looking it up. Its 404 must
            // still have the same shape as the handler's genuine "customer not found", which a
            // caller whose own customer record doesn't exist receives.
            var otherCustomerId = await CreateCustomerAsync($"{Guid.NewGuid()}@example.com");
            var callerWithoutRecord = Guid.NewGuid();
            using var caller = AsCustomer(callerWithoutRecord);

            var refusedByFilter = await caller.GetAsync($"/api/v1/customers/{otherCustomerId}");
            var missing = await caller.GetAsync($"/api/v1/customers/{callerWithoutRecord}");

            await AssertIndistinguishableAsync(refusedByFilter, otherCustomerId.ToString(), missing, callerWithoutRecord.ToString());
        }

        private static async Task AssertIndistinguishableAsync(
            HttpResponseMessage notOwned, string notOwnedKey, HttpResponseMessage missing, string missingKey)
        {
            notOwned.StatusCode.Should().Be(HttpStatusCode.NotFound);
            missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
            notOwned.Content.Headers.ContentType?.MediaType
                .Should().Be(missing.Content.Headers.ContentType?.MediaType);

            var notOwnedBody = await NormalizedBodyAsync(notOwned, notOwnedKey);
            var missingBody = await NormalizedBodyAsync(missing, missingKey);

            notOwnedBody.Should().NotBeEmpty("every 404 must be a ProblemDetails body, never an empty response");
            notOwnedBody.Should().Be(missingBody,
                "a resource owned by someone else must look exactly like one that doesn't exist");
        }

        /// <summary>The body with its per-request traceId dropped and the looked-up key replaced by a placeholder.</summary>
        private static async Task<string> NormalizedBodyAsync(HttpResponseMessage response, string key)
        {
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            var json = JsonNode.Parse(content)!.AsObject();
            json.Remove("traceId");
            return json.ToJsonString().Replace(key, "{key}", StringComparison.OrdinalIgnoreCase);
        }

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

        private async Task<(HttpClient Attacker, Guid AttackerId, Guid VictimAccountId)> AttackerAndVictimAccountAsync()
        {
            var ownerId = await CreateCustomerAsync($"{Guid.NewGuid()}@example.com");
            using var owner = AsCustomer(ownerId);
            var createAccount = await owner.PostAsJsonAsync(
                $"/api/v1/customers/{ownerId}/accounts",
                new { AccountNumber = $"acc-{Guid.NewGuid():N}", AccountName = "Everyday", AccountType = AccountType.Checking, Currency = "ZAR" });
            createAccount.EnsureSuccessStatusCode();
            var victimAccountId = await createAccount.Content.ReadFromJsonAsync<Guid>();

            var attackerId = await CreateCustomerAsync($"{Guid.NewGuid()}@example.com");
            return (AsCustomer(attackerId), attackerId, victimAccountId);
        }

        private async Task<Guid> SeedTransactionAsync(Guid customerId)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();

            var transaction = Transaction.Create(
                CustomerId.CreateFrom(customerId),
                Money.Create(-42.00m, "ZAR"),
                "victim purchase",
                TransactionCategory.Uncategorized,
                TransactionSource.Create("TestBank", Guid.NewGuid().ToString()));

            context.Transactions.Add(transaction);
            await context.SaveChangesAsync();
            return transaction.Id.Value;
        }
    }
}