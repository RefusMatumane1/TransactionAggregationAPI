using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    public class CustomerApiIntegrationTests : IClassFixture<IntegrationTestWebAppFactory>
    {
        private readonly HttpClient _client;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public CustomerApiIntegrationTests(IntegrationTestWebAppFactory factory)
        {
            _client = factory.CreateClient();
        }

        /// <summary>
        /// Creates a customer, logs in, and attaches the JWT to the client's default headers.
        /// Returns the new customer's ID.
        /// </summary>
        private async Task<Guid> CreateAndAuthenticateAsync(string email)
        {
            var createRequest = new { Email = email, Name = "Test User", Password = "Password1" };
            var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", createRequest);
            createResponse.EnsureSuccessStatusCode();
            var customerId = await createResponse.Content.ReadFromJsonAsync<Guid>();

            var loginRequest = new { Username = email, Password = "Password1" };
            var loginResponse = await _client.PostAsJsonAsync("/api/v1/customers/login", loginRequest);
            loginResponse.EnsureSuccessStatusCode();
            var token = await loginResponse.Content.ReadFromJsonAsync<string>();

            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token!);

            return customerId;
        }

        // ── Customer CRUD ─────────────────────────────────────────────────────

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

            // A random GUID that does not match the authenticated user's ID
            var response = await _client.GetAsync($"/api/v1/customers/{Guid.NewGuid()}");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task GetAllCustomers_Returns200WithPaginatedResult()
        {
            await CreateAndAuthenticateAsync("allcustomers@example.com");

            var response = await _client.GetAsync("/api/v1/customers?page=1&pageSize=10");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var json = await response.Content.ReadAsStringAsync();
            json.Should().Contain("items");
            json.Should().Contain("totalCount");
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
        public async Task DeleteCustomer_ExistingCustomer_Returns204()
        {
            var id = await CreateAndAuthenticateAsync("delete@example.com");

            var response = await _client.DeleteAsync($"/api/v1/customers/{id}");

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // ── Transactions ──────────────────────────────────────────────────────

        [Fact]
        public async Task CreateTransaction_ForExistingCustomer_Returns201()
        {
            var customerId = await CreateAndAuthenticateAsync("tx@example.com");

            var txRequest = new
            {
                Amount = -150.00m,
                Currency = "ZAR",
                TransactionDate = DateTime.UtcNow,
                Description = "grocery store purchase",
                SourceSystem = "TestBank"
            };

            var response = await _client.PostAsJsonAsync(
                $"/api/v1/customers/{customerId}/transactions", txRequest);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task FilterTransactions_ReturnsPagedResult()
        {
            var customerId = await CreateAndAuthenticateAsync("filter@example.com");

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

            var txRequest = new
            {
                Amount = -200.00m,
                Currency = "ZAR",
                TransactionDate = DateTime.UtcNow,
                Description = "rent payment",
                SourceSystem = "TestBank"
            };
            await _client.PostAsJsonAsync($"/api/v1/customers/{customerId}/transactions", txRequest);

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

            var txRequest = new
            {
                Amount = -50.00m,
                Currency = "ZAR",
                TransactionDate = DateTime.UtcNow,
                Description = "mystery purchase",
                SourceSystem = "TestBank"
            };
            var txResponse = await _client.PostAsJsonAsync(
                $"/api/v1/customers/{customerId}/transactions", txRequest);
            var txId = await txResponse.Content.ReadFromJsonAsync<Guid>();

            var catRequest = new { Category = 2 }; // Dining = 2
            var response = await _client.PatchAsJsonAsync(
                $"/api/v1/transactions/{txId}/categorize", catRequest);

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }
}
