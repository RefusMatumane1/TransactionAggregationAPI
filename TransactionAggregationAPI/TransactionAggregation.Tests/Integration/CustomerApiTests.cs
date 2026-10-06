using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Transactions.Infrastructure.Persistence;
using Modules.WebhookSources.Domain;
using Modules.WebhookSources.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Integration
{
    // Customers through the HTTP surface: admins register customers and link their bank accounts;
    // staff read a customer's transactions and aggregates across banks, but only at the banks they
    // are assigned, and a customer they can't see is a 404 exactly like a missing one (BOLA).
    public class CustomerApiTests(IntegrationTestWebAppFactory factory) : IClassFixture<IntegrationTestWebAppFactory>
    {
        private sealed record Seeded(Guid CrossBank, Guid CapitecOnly, string FnbAccount, string CapitecAccount);

        private async Task<Seeded> SeedAsync()
        {
            var run = Guid.NewGuid().ToString("N")[..10];
            var fnbAccount = $"fnb-{run}";
            var capitecAccount = $"cap-{run}";
            var otherCapitec = $"cap2-{run}";

            using (var scope = factory.Services.CreateScope())
            {
                var banks = scope.ServiceProvider.GetRequiredService<WebhookSourcesDbContext>();
                foreach (var code in new[] { TestInstitutions.FNB, TestInstitutions.Capitec })
                {
                    if (!await banks.WebhookSources.AnyAsync(s => s.Name == code))
                        banks.WebhookSources.Add(WebhookSource.Create(code, code, WebhookSource.DefaultColor).Source);
                }
                await banks.SaveChangesAsync();

                var ledger = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
                ledger.Transactions.AddRange(
                    TestTransactions.Create(-100m, $"FNB spend {run}", institution: TestInstitutions.FNB, account: fnbAccount),
                    TestTransactions.Create(-40m, $"Capitec spend {run}", institution: TestInstitutions.Capitec, account: capitecAccount),
                    TestTransactions.Create(-999m, $"Not theirs {run}", institution: TestInstitutions.FNB, account: $"stranger-{run}"));
                await ledger.SaveChangesAsync();
                await InMemoryDailyTotals.BuildAsync(ledger);
            }

            using var admin = factory.CreateClient().SignedInAs("admin");
            var crossBank = await CreateAsync(admin, $"X-{run}");
            (await LinkAsync(admin, crossBank, TestInstitutions.FNB, fnbAccount)).StatusCode.Should().Be(HttpStatusCode.Created);
            (await LinkAsync(admin, crossBank, TestInstitutions.Capitec, capitecAccount)).StatusCode.Should().Be(HttpStatusCode.Created);

            var capitecOnly = await CreateAsync(admin, $"C-{run}");
            (await LinkAsync(admin, capitecOnly, TestInstitutions.Capitec, otherCapitec)).StatusCode.Should().Be(HttpStatusCode.Created);

            return new Seeded(crossBank, capitecOnly, fnbAccount, capitecAccount);
        }

        private static async Task<Guid> CreateAsync(HttpClient admin, string reference)
        {
            var response = await admin.PostAsJsonAsync("/api/v1/customers", new { reference, name = $"Customer {reference}" });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        private static Task<HttpResponseMessage> LinkAsync(HttpClient admin, Guid customer, string institution, string account) =>
            admin.PostAsJsonAsync($"/api/v1/customers/{customer}/accounts", new { institution, externalAccountId = account });

        private static async Task<List<string>> DescriptionsAsync(HttpClient client, Guid customer)
        {
            var json = await client.GetFromJsonAsync<JsonElement>($"/api/v1/customers/{customer}/transactions?pageSize=100");
            return json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("description").GetString()!).ToList();
        }

        [Fact]
        public async Task ACustomersTransactions_SpanEveryLinkedBank_AndNoOneElses()
        {
            var seeded = await SeedAsync();
            using var admin = factory.CreateClient().SignedInAs("admin");

            var descriptions = await DescriptionsAsync(admin, seeded.CrossBank);

            descriptions.Should().HaveCount(2).And.OnlyContain(d => d.StartsWith("FNB spend") || d.StartsWith("Capitec spend"));
        }

        [Fact]
        public async Task ACustomersAggregates_TotalTheirAccountsAcrossBanks()
        {
            var seeded = await SeedAsync();
            using var admin = factory.CreateClient().SignedInAs("admin");

            var json = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/customers/{seeded.CrossBank}/aggregates/institutions");

            var banks = json.GetProperty("institutions").EnumerateArray()
                .Select(i => (i.GetProperty("institution").GetString(), i.GetProperty("expenses").GetDecimal()))
                .ToList();
            banks.Should().BeEquivalentTo([(TestInstitutions.FNB, 100m), (TestInstitutions.Capitec, 40m)],
                "the stranger's FNB account is not the customer's");
        }

        [Fact]
        public async Task Staff_SeeOnlyTheirBanksSliceOfACustomer()
        {
            var seeded = await SeedAsync();
            using var staff = factory.CreateClient().SignedInAsStaffFor(TestInstitutions.FNB);

            var customer = await staff.GetFromJsonAsync<JsonElement>($"/api/v1/customers/{seeded.CrossBank}");
            var descriptions = await DescriptionsAsync(staff, seeded.CrossBank);

            customer.GetProperty("accounts").EnumerateArray().Select(a => a.GetProperty("institution").GetString())
                .Should().Equal(TestInstitutions.FNB);
            descriptions.Should().ContainSingle().Which.Should().StartWith("FNB spend");
        }

        [Theory]
        [InlineData("")]
        [InlineData("/transactions")]
        [InlineData("/aggregates/cash-flow")]
        [InlineData("/aggregates/categories")]
        [InlineData("/aggregates/institutions")]
        [InlineData("/aggregates/comparison")]
        public async Task ACustomerAtNoneOfTheStaffsBanks_IsNotFound_ExactlyLikeAMissingOne(string path)
        {
            var seeded = await SeedAsync();
            using var staff = factory.CreateClient().SignedInAsStaffFor(TestInstitutions.FNB);

            var hidden = await staff.GetAsync($"/api/v1/customers/{seeded.CapitecOnly}{path}");
            var missing = await staff.GetAsync($"/api/v1/customers/{Guid.NewGuid()}{path}");

            hidden.StatusCode.Should().Be(HttpStatusCode.NotFound);
            missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Linking_IsIdempotent_RejectsUnknownBanks_AndUnlinkingRemovesTheAccountFromTheView()
        {
            var seeded = await SeedAsync();
            using var admin = factory.CreateClient().SignedInAs("admin");

            var again = await LinkAsync(admin, seeded.CrossBank, TestInstitutions.FNB, seeded.FnbAccount);
            var unknownBank = await LinkAsync(admin, seeded.CrossBank, "NoSuchBank", "x-1");
            var unlinked = await admin.DeleteAsync(
                $"/api/v1/customers/{seeded.CrossBank}/accounts?institution={TestInstitutions.Capitec}&externalAccountId={seeded.CapitecAccount}");
            var unlinkedAgain = await admin.DeleteAsync(
                $"/api/v1/customers/{seeded.CrossBank}/accounts?institution={TestInstitutions.Capitec}&externalAccountId={seeded.CapitecAccount}");

            again.StatusCode.Should().Be(HttpStatusCode.OK, "linking an already linked account changes nothing");
            unknownBank.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            unlinked.StatusCode.Should().Be(HttpStatusCode.NoContent);
            unlinkedAgain.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await DescriptionsAsync(admin, seeded.CrossBank)).Should().ContainSingle().Which.Should().StartWith("FNB spend");
        }

        [Fact]
        public async Task Staff_CannotRegisterCustomersOrChangeLinks()
        {
            var seeded = await SeedAsync();
            using var staff = factory.CreateClient().SignedInAsStaffFor(TestInstitutions.FNB);

            var create = await staff.PostAsJsonAsync("/api/v1/customers", new { reference = "NOPE-1", name = "Nope" });
            var link = await LinkAsync(staff, seeded.CrossBank, TestInstitutions.FNB, "x-2");
            var unlink = await staff.DeleteAsync(
                $"/api/v1/customers/{seeded.CrossBank}/accounts?institution={TestInstitutions.FNB}&externalAccountId={seeded.FnbAccount}");

            create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            link.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            unlink.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task ADuplicateReference_IsAConflict_AndAMalformedOneIsAValidationProblem()
        {
            using var admin = factory.CreateClient().SignedInAs("admin");
            var reference = $"DUP-{Guid.NewGuid():N}"[..20];

            await CreateAsync(admin, reference);
            var duplicate = await admin.PostAsJsonAsync("/api/v1/customers", new { reference, name = "Second" });
            var malformed = await admin.PostAsJsonAsync("/api/v1/customers", new { reference = "has space", name = "" });

            duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
            malformed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var errors = (await malformed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
            errors.TryGetProperty("reference", out _).Should().BeTrue();
            errors.TryGetProperty("name", out _).Should().BeTrue();
        }
    }
}