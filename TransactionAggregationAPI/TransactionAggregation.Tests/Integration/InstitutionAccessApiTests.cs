using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Modules.Transactions.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    // Object-level authorization (BOLA/IDOR): a staff member reads only the institutions assigned
    // to them, through every read path, and cannot widen that by changing ids or filters.
    public class InstitutionAccessApiTests(IntegrationTestWebAppFactory factory) : IClassFixture<IntegrationTestWebAppFactory>
    {
        private async Task<(Guid Fnb, Guid Capitec, string Account)> SeedAsync()
        {
            var account = $"bola-{Guid.NewGuid():N}";
            var fnb = TestTransactions.Create(-10m, "FNB purchase", institution: TestInstitutions.FNB, account: account);
            var capitec = TestTransactions.Create(-20m, "Capitec purchase", institution: TestInstitutions.Capitec, account: account);

            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
            context.Transactions.AddRange(fnb, capitec);
            await context.SaveChangesAsync();

            // What the worker's scheduled refresh does. (No cache to clear: this host runs without Redis.)
            await InMemoryDailyTotals.BuildAsync(context);
            return (fnb.Id.Value, capitec.Id.Value, account);
        }

        private static async Task<List<string>> DescriptionsAsync(HttpClient client, string query)
        {
            var json = await client.GetFromJsonAsync<JsonElement>($"/api/v1/transactions?pageSize=100&{query}");
            return json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("description").GetString()!).ToList();
        }

        [Fact]
        public async Task GetById_AnotherInstitutionsTransaction_IsNotFound_ExactlyLikeAMissingOne()
        {
            var (fnb, capitec, _) = await SeedAsync();
            using var staff = factory.CreateClient().SignedInAsStaffFor(TestInstitutions.FNB);

            (await staff.GetAsync($"/api/v1/transactions/{fnb}")).StatusCode.Should().Be(HttpStatusCode.OK);
            var foreign = await staff.GetAsync($"/api/v1/transactions/{capitec}");
            var missing = await staff.GetAsync($"/api/v1/transactions/{Guid.NewGuid()}");

            foreign.StatusCode.Should().Be(HttpStatusCode.NotFound);
            missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task List_ShowsOnlyTheCallersInstitutions_EvenWhenTheyFilterForAnother()
        {
            var (_, _, account) = await SeedAsync();
            using var staff = factory.CreateClient().SignedInAsStaffFor(TestInstitutions.FNB);

            (await DescriptionsAsync(staff, $"institution={TestInstitutions.FNB}&externalAccountId={account}"))
                .Should().Equal("FNB purchase");
            (await DescriptionsAsync(staff, $"institution={TestInstitutions.Capitec}&externalAccountId={account}"))
                .Should().BeEmpty("a filter can only narrow what the caller may read");
        }

        [Fact]
        public async Task StaffWithNoInstitutionsAssigned_SeeNothing()
        {
            var (_, capitec, account) = await SeedAsync();
            using var staff = factory.CreateClient().SignedInAs("staff");

            (await DescriptionsAsync(staff, $"institution={TestInstitutions.Capitec}&externalAccountId={account}")).Should().BeEmpty();
            (await staff.GetAsync($"/api/v1/transactions/{capitec}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Admins_ReadEveryInstitution()
        {
            var (fnb, capitec, account) = await SeedAsync();
            using var admin = factory.CreateClient().SignedInAs("admin");

            (await admin.GetAsync($"/api/v1/transactions/{fnb}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await admin.GetAsync($"/api/v1/transactions/{capitec}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await DescriptionsAsync(admin, $"institution={TestInstitutions.Capitec}&externalAccountId={account}"))
                .Should().Equal("Capitec purchase");
        }

        [Fact]
        public async Task Aggregates_CountOnlyTheCallersInstitutions()
        {
            var (_, _, account) = await SeedAsync();
            using var staff = factory.CreateClient().SignedInAsStaffFor(TestInstitutions.FNB);

            var json = await staff.GetFromJsonAsync<JsonElement>("/api/v1/transactions/aggregates/institutions");

            var institutions = json.GetProperty("institutions").EnumerateArray()
                .Select(i => i.GetProperty("institution").GetString())
                .ToList();
            institutions.Should().Contain(TestInstitutions.FNB);
            institutions.Should().NotContain(TestInstitutions.Capitec);
            account.Should().NotBeEmpty();
        }

        [Fact]
        public async Task CachedReads_AreNeverServedAcrossScopes()
        {
            var (_, _, account) = await SeedAsync();
            var query = $"externalAccountId={account}&institution={TestInstitutions.Capitec}";
            using var admin = factory.CreateClient().SignedInAs("admin");
            using var staff = factory.CreateClient().SignedInAsStaffFor(TestInstitutions.FNB);

            (await DescriptionsAsync(admin, query)).Should().Equal("Capitec purchase");
            (await DescriptionsAsync(staff, query)).Should().BeEmpty("the caller's access is part of the cache key");
        }

        [Fact]
        public async Task AnAccountWithoutItsInstitution_IsRejected()
        {
            using var admin = factory.CreateClient().SignedInAs("admin");

            var response = await admin.GetAsync("/api/v1/transactions?externalAccountId=acc-1");

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("institution");
        }
    }
}