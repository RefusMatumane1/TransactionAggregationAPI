using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Modules.Transactions.Domain.Enums;
using Modules.Transactions.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    // There are no customer accounts: every signed-in user is staff or admin. Staff read transactions
    // and aggregates; only admins change data or reach the admin areas.
    public class RoleAccessTests : IClassFixture<IntegrationTestWebAppFactory>
    {
        private readonly IntegrationTestWebAppFactory _factory;

        public RoleAccessTests(IntegrationTestWebAppFactory factory)
        {
            _factory = factory;
        }

        public static TheoryData<string> ReadEndpoints => new()
        {
            "/api/v1/transactions",
            "/api/v1/transactions/summary",
            "/api/v1/transactions/aggregates/categories",
            "/api/v1/transactions/aggregates/cash-flow",
            "/api/v1/transactions/aggregates/institutions",
            "/api/v1/banks",
            "/api/v1/transactions/aggregates/comparison"
        };

        public static TheoryData<string> AdminEndpoints => new()
        {
            "/api/v1/admin/webhook-sources",
            "/api/v1/admin/audit/events"
        };

        private async Task<Guid> SeedTransactionAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
            var transaction = TestTransactions.Create(-42m, "Role test");
            context.Transactions.Add(transaction);
            await context.SaveChangesAsync();
            return transaction.Id.Value;
        }

        [Theory]
        [MemberData(nameof(ReadEndpoints))]
        public async Task Reads_RequireSignIn(string path)
        {
            using var anonymous = _factory.CreateClient();

            (await anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Theory]
        [MemberData(nameof(ReadEndpoints))]
        public async Task Reads_AreRefusedWithoutTheStaffOrAdminRole(string path)
        {
            using var noRole = _factory.CreateClient().SignedInAs();

            (await noRole.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Theory]
        [MemberData(nameof(ReadEndpoints))]
        public async Task Reads_AreOpenToStaffAndAdmins(string path)
        {
            using var staff = _factory.CreateClient().SignedInAs("staff");
            using var admin = _factory.CreateClient().SignedInAs("admin");

            (await staff.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await admin.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Theory]
        [MemberData(nameof(AdminEndpoints))]
        public async Task AdminAreas_AreRefusedToStaff(string path)
        {
            using var staff = _factory.CreateClient().SignedInAs("staff");

            (await staff.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetTransactionById_IsOpenToStaff()
        {
            var id = await SeedTransactionAsync();
            using var staff = _factory.CreateClient().SignedInAsStaffFor(TestInstitutions.FNB);

            (await staff.GetAsync($"/api/v1/transactions/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Transactions_CannotBeChanged_EvenByAnAdmin()
        {
            var id = await SeedTransactionAsync();
            using var admin = _factory.CreateClient().SignedInAs("admin");

            (await admin.PatchAsJsonAsync($"/api/v1/transactions/{id}/categorize", new { Category = TransactionCategory.Groceries }))
                .StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
            (await admin.PatchAsJsonAsync($"/api/v1/transactions/{id}", new { Category = TransactionCategory.Groceries }))
                .StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        }

        [Fact]
        public async Task CustomerEndpoints_NoLongerExist()
        {
            using var admin = _factory.CreateClient().SignedInAs("admin");

            (await admin.GetAsync($"/api/v1/customers/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await admin.GetAsync($"/api/v1/customers/{Guid.NewGuid()}/bank-links")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}