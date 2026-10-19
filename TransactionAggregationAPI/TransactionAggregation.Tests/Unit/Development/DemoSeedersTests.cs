using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Customers.Infrastructure.Development;
using Modules.Transactions.Contracts;
using Modules.Transactions.Infrastructure.Development;
using NSubstitute;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Development
{
    public class DemoSeedersTests
    {
        private static IConfiguration Config(int randomSeed = 7) => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [TransactionsDemoSeeder.RandomSeedKey] = randomSeed.ToString() })
            .Build();

        private static readonly string[] CatalogAccounts =
            [.. SeedCatalog.Households.SelectMany(h => h.Accounts).Select(a => $"{a.Institution}/{a.ExternalAccountId}")];

        [Fact]
        public void TheDemoAccountDirectory_ListsExactlyTheAccountsTheTransactionSeedCreates()
        {
            var directory = new DemoAccountDirectory();

            directory.Households.Should().HaveCount(SeedCatalog.Households.Length);
            directory.Households.SelectMany(h => h).Select(a => $"{a.Institution}/{a.ExternalAccountId}")
                .Should().BeEquivalentTo(CatalogAccounts);
        }

        [Fact]
        public async Task TheTransactionSeed_GivesEveryDemoAccountItsHistory_InTheBaseCurrency()
        {
            using var context = InMemoryDbContextFactory.Create();
            var seeder = new TransactionsDemoSeeder(context, Config(), TimeProvider.System, TestCurrencies.Catalog, NullLogger<TransactionsDemoSeeder>.Instance);

            await seeder.SeedAsync(CancellationToken.None);

            var seeded = await context.Transactions.AsNoTracking().ToListAsync();
            seeded.Select(t => $"{t.Source.Name}/{t.ExternalAccountId}").Distinct().Should().BeEquivalentTo(CatalogAccounts);
            seeded.Should().OnlyContain(t => t.Amount.Currency == TestCurrencies.Catalog.BaseCurrency);
        }

        [Fact]
        public async Task SeedingAgain_OnlyTopsUpRecentActivity_AndDoesNotBackfillTheHistoryTwice()
        {
            using var context = InMemoryDbContextFactory.Create();
            var seeder = new TransactionsDemoSeeder(context, Config(), TimeProvider.System, TestCurrencies.Catalog, NullLogger<TransactionsDemoSeeder>.Instance);

            await seeder.SeedAsync(CancellationToken.None);
            var first = await context.Transactions.CountAsync();
            await seeder.SeedAsync(CancellationToken.None);
            var second = await context.Transactions.CountAsync();

            (second - first).Should().BeLessThan(first / 4, "a second run adds recent activity, not another fifteen months");
        }

        private static IDemoAccountDirectory Directory(params DemoAccount[][] households)
        {
            var directory = Substitute.For<IDemoAccountDirectory>();
            directory.Households.Returns([.. households.Select(h => (IReadOnlyList<DemoAccount>)h)]);
            return directory;
        }

        private static readonly DemoAccount[][] TwoHouseholds =
        [
            [new("FNB", "acc-1"), new("Absa", "acc-2")],
            [new("Capitec", "acc-3")]
        ];

        [Fact]
        public async Task TheCustomerSeed_CreatesOneCustomerPerHousehold_AndLinksTheirAccounts()
        {
            var db = Guid.NewGuid().ToString("N");
            using (var context = InMemoryCustomersDbContextFactory.Create(db))
                await new CustomersDemoSeeder(context, Directory(TwoHouseholds), TimeProvider.System, NullLogger<CustomersDemoSeeder>.Instance)
                    .SeedAsync(CancellationToken.None);

            using var verify = InMemoryCustomersDbContextFactory.Create(db);
            var customers = await verify.Customers.AsNoTracking().Include(c => c.Accounts).ToListAsync();

            customers.Select(c => c.Reference).Should().BeEquivalentTo("CUST-0001", "CUST-0002", "CUST-0101", "CUST-0102", "CUST-0103");
            customers.Single(c => c.Reference == "CUST-0001").Accounts.Select(a => $"{a.Institution}/{a.ExternalAccountId}")
                .Should().BeEquivalentTo("FNB/acc-1", "Absa/acc-2");
            customers.Single(c => c.Reference == "CUST-0002").Accounts.Should().ContainSingle();
        }

        [Fact]
        public async Task TheCustomerSeed_CanRunAgainWithoutDuplicatingAnything()
        {
            var db = Guid.NewGuid().ToString("N");
            for (var run = 0; run < 2; run++)
            {
                using var context = InMemoryCustomersDbContextFactory.Create(db);
                await new CustomersDemoSeeder(context, Directory(TwoHouseholds), TimeProvider.System, NullLogger<CustomersDemoSeeder>.Instance)
                    .SeedAsync(CancellationToken.None);
            }

            using var verify = InMemoryCustomersDbContextFactory.Create(db);
            (await verify.Customers.CountAsync()).Should().Be(5);
            (await verify.Customers.AsNoTracking().Include(c => c.Accounts).ToListAsync()).Sum(c => c.Accounts.Count).Should().Be(3 + 8);
        }
    }
}
