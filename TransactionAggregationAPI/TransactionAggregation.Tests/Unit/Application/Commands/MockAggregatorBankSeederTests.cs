using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.WebhookSources.Domain;
using Modules.WebhookSources.Infrastructure.Development;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands
{
    public class MockAggregatorBankSeederTests
    {
        private const string Key = "dev-only-mock-aggregator-webhook-key-not-a-secret";

        private readonly string _dbName = Guid.NewGuid().ToString();

        private async Task SeedAsync(string? key)
        {
            await using var db = InMemoryWebhookSourcesDbContextFactory.Create(_dbName);
            await new MockAggregatorBankSeeder(db, Config(key), NullLogger<MockAggregatorBankSeeder>.Instance)
                .SeedAsync(CancellationToken.None);
        }

        private static IConfiguration Config(string? key) => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MockAggregator:WebhookApiKey"] = key })
            .Build();

        private async Task<List<WebhookSource>> StoredSourcesAsync()
        {
            using var db = InMemoryWebhookSourcesDbContextFactory.Create(_dbName);
            return await db.WebhookSources.ToListAsync();
        }

        [Fact]
        public async Task RegistersEveryMockBank_EachWithItsOwnKey()
        {
            await SeedAsync(Key);

            var sources = await StoredSourcesAsync();
            sources.Select(s => s.Name).Should().BeEquivalentTo(MockAggregatorBankSeeder.Banks.Select(b => b.Code));
            foreach (var bank in MockAggregatorBankSeeder.Banks)
            {
                var source = sources.Single(s => s.Name == bank.Code);
                source.IsActive.Should().BeTrue();
                source.DisplayName.Should().Be(bank.DisplayName);
                source.HasKey(MockAggregatorBankSeeder.KeyFor(Key, bank.Code)).Should().BeTrue();
                source.KeyHash.Should().NotContain(Key, "only the hash is stored");
            }
            sources.Select(s => s.KeyHash).Should().OnlyHaveUniqueItems("one bank's key must not work for another");
        }

        [Fact]
        public async Task ChangedKeyOrDeactivatedBank_IsPutRightOnTheNextStartup()
        {
            await SeedAsync(Key);
            await using (var db = InMemoryWebhookSourcesDbContextFactory.Create(_dbName))
            {
                (await db.WebhookSources.SingleAsync(s => s.Name == "FNB")).Deactivate();
                await db.SaveChangesAsync();
            }

            const string newKey = "dev-only-mock-aggregator-webhook-key-rotated-0001";
            await SeedAsync(newKey);

            var fnb = (await StoredSourcesAsync()).Single(s => s.Name == "FNB");
            fnb.IsActive.Should().BeTrue();
            fnb.HasKey(MockAggregatorBankSeeder.KeyFor(newKey, "FNB")).Should().BeTrue();
            fnb.HasKey(MockAggregatorBankSeeder.KeyFor(Key, "FNB")).Should().BeFalse();
        }

        [Fact]
        public async Task NoConfiguredKey_RegistersNothing()
        {
            await SeedAsync(null);

            (await StoredSourcesAsync()).Should().BeEmpty();
        }
    }
}