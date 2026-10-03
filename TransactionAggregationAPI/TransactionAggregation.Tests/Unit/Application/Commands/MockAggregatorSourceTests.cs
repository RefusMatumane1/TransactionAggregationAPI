using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.WebhookSources.Domain;
using TransactionAggregation.Tests.Helpers;
using TransactionAggregationAPI.Development;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands
{
    public class MockAggregatorSourceTests
    {
        private const string Key = "dev-only-mock-aggregator-webhook-key-not-a-secret";

        private readonly string _dbName = Guid.NewGuid().ToString();

        private IServiceProvider Services()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddScoped(_ => InMemoryWebhookSourcesDbContextFactory.Create(_dbName));
            return services.BuildServiceProvider();
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
            await MockAggregatorSource.EnsureRegisteredAsync(Services(), Config(Key));

            var sources = await StoredSourcesAsync();
            sources.Select(s => s.Name).Should().BeEquivalentTo(MockAggregatorSource.Banks.Select(b => b.Code));
            foreach (var bank in MockAggregatorSource.Banks)
            {
                var source = sources.Single(s => s.Name == bank.Code);
                source.IsActive.Should().BeTrue();
                source.DisplayName.Should().Be(bank.DisplayName);
                source.HasKey(MockAggregatorSource.KeyFor(Key, bank.Code)).Should().BeTrue();
                source.KeyHash.Should().NotContain(Key, "only the hash is stored");
            }
            sources.Select(s => s.KeyHash).Should().OnlyHaveUniqueItems("one bank's key must not work for another");
        }

        [Fact]
        public async Task ChangedKeyOrDeactivatedBank_IsPutRightOnTheNextStartup()
        {
            var services = Services();
            await MockAggregatorSource.EnsureRegisteredAsync(services, Config(Key));
            await using (var db = InMemoryWebhookSourcesDbContextFactory.Create(_dbName))
            {
                (await db.WebhookSources.SingleAsync(s => s.Name == "FNB")).Deactivate();
                await db.SaveChangesAsync();
            }

            const string newKey = "dev-only-mock-aggregator-webhook-key-rotated-0001";
            await MockAggregatorSource.EnsureRegisteredAsync(services, Config(newKey));

            var fnb = (await StoredSourcesAsync()).Single(s => s.Name == "FNB");
            fnb.IsActive.Should().BeTrue();
            fnb.HasKey(MockAggregatorSource.KeyFor(newKey, "FNB")).Should().BeTrue();
            fnb.HasKey(MockAggregatorSource.KeyFor(Key, "FNB")).Should().BeFalse();
        }

        [Fact]
        public async Task NoConfiguredKey_RegistersNothing()
        {
            await MockAggregatorSource.EnsureRegisteredAsync(Services(), Config(null));

            (await StoredSourcesAsync()).Should().BeEmpty();
        }
    }
}