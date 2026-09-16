using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;
using TransactionAggregation.Persistence;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    public class CustomerApiIntegrationTests : IClassFixture<IntegrationTestWebAppFactory>
    {
        private readonly IntegrationTestWebAppFactory _factory;
        private readonly HttpClient _client;

        public CustomerApiIntegrationTests(IntegrationTestWebAppFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task<Transaction> SeedTransactionAsync(
    Guid customerId, decimal amount = -150.00m, string description = "grocery store purchase")
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var transaction = Transaction.Create(
                CustomerId.CreateFrom(customerId),
                Money.Create(amount, "ZAR"),
                description,
                TransactionCategory.Uncategorized,
                TransactionSource.Create("TestBank", Guid.NewGuid().ToString()));

            context.Transactions.Add(transaction);
            await context.SaveChangesAsync();
            return transaction;
        }

        private async Task<Guid> CreateAndAuthenticateAsync(string email)
        {
            var createRequest = new { Email = email, Name = "Test User", Password = "Password1" };
            var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", createRequest);
            createResponse.EnsureSuccessStatusCode();
            var customerId = await createResponse.Content.ReadFromJsonAsync<Guid>();

            _client.DefaultRequestHeaders.Remove(TestAuthHandler.UserIdHeaderName);
            _client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeaderName, customerId.ToString());

            return customerId;
        }

        [Fact]
        public async Task CreateCustomer_WithValidData_Returns201AndId()
        {
            var request = new { Email = "test@example.com", Name = "Test User", Password = "Password1" };

            var response = await _client.PostAsJsonAsync("/api/v1/customers", request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var id = await response.Content.ReadFromJsonAsync<Guid>();
            id.Should().NotBeEmpty();
        }

        [Fact]
        public async Task CreateCustomer_WithDuplicateEmail_Returns409()
        {
            var request = new { Email = "duplicate@example.com", Name = "User One", Password = "Password1" };
            await _client.PostAsJsonAsync("/api/v1/customers", request);

            var response = await _client.PostAsJsonAsync("/api/v1/customers", request);

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task GetCustomerById_AfterCreate_Returns200WithCustomer()
        {
            var id = await CreateAndAuthenticateAsync("getbyid@example.com");

            var response = await _client.GetAsync($"/api/v1/customers/{id}");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var json = await response.Content.ReadAsStringAsync();
            json.Should().Contain("getbyid@example.com");
        }

        [Fact]
        public async Task GetCustomerById_WithUnknownId_Returns404()
        {
            await CreateAndAuthenticateAsync("unknown@example.com");

            var response = await _client.GetAsync($"/api/v1/customers/{Guid.NewGuid()}");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task UpdateCustomer_WithValidData_Returns204()
        {
            var id = await CreateAndAuthenticateAsync("update@example.com");

            var updateRequest = new { Email = "updated@example.com", Name = "Updated Name" };
            var response = await _client.PutAsJsonAsync($"/api/v1/customers/{id}", updateRequest);

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        [Fact]
        public async Task FilterTransactions_ReturnsPagedResult()
        {
            var customerId = await CreateAndAuthenticateAsync("filter@example.com");
            await SeedTransactionAsync(customerId);

            var response = await _client.GetAsync(
                $"/api/v1/customers/{customerId}/transactions/filter?pageNumber=1&pageSize=10");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var json = await response.Content.ReadAsStringAsync();
            json.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public async Task GetTransactionSummary_ReturnsSpendBreakdown()
        {
            var customerId = await CreateAndAuthenticateAsync("summary@example.com");
            await SeedTransactionAsync(customerId, amount: -200.00m, description: "rent payment");

            var response = await _client.GetAsync(
                $"/api/v1/customers/{customerId}/transactions/summary");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var json = await response.Content.ReadAsStringAsync();
            json.Should().Contain("totalExpenses");
            json.Should().Contain("spendingByCategory");
            json.Should().Contain("monthlySummaries");
        }

        [Fact]
        public async Task CategorizeTransaction_Returns204()
        {
            var customerId = await CreateAndAuthenticateAsync("cat@example.com");
            var transaction = await SeedTransactionAsync(customerId, amount: -50.00m, description: "mystery purchase");

            var catRequest = new { Category = 2 };
            var response = await _client.PatchAsJsonAsync(
                $"/api/v1/transactions/{transaction.Id.Value}/categorize", catRequest);

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }
}