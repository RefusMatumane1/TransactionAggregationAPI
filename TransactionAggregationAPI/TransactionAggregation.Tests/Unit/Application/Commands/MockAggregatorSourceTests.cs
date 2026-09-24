using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.WebhookSources.Domain;
using TransactionAggregation.Tests.Helpers;
using TransactionAggregationAPI;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Application.Commands;

/// <summary>The Development-only registration of the webhook source the mock aggregator sends as.</summary>
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

    private async Task<WebhookSource?> StoredSourceAsync()
    {
        using var db = InMemoryWebhookSourcesDbContextFactory.Create(_dbName);
        return await db.WebhookSources.SingleOrDefaultAsync(s => s.Name == "mock-aggregator");
    }

    [Fact]
    public async Task RegistersTheSourceWithTheConfiguredKey()
    {
        await MockAggregatorSource.EnsureRegisteredAsync(Services(), Config(Key));

        var source = await StoredSourceAsync();
        source!.IsActive.Should().BeTrue();
        source.HasKey(Key).Should().BeTrue();
        source.KeyHash.Should().NotContain(Key, "only the hash is stored");
    }

    [Fact]
    public async Task ChangedKeyOrDeactivatedSource_IsPutRightOnTheNextStartup()
    {
        var services = Services();
        await MockAggregatorSource.EnsureRegisteredAsync(services, Config(Key));
        await using (var db = InMemoryWebhookSourcesDbContextFactory.Create(_dbName))
        {
            (await db.WebhookSources.SingleAsync()).Deactivate();
            await db.SaveChangesAsync();
        }

        const string newKey = "dev-only-mock-aggregator-webhook-key-rotated-0001";
        await MockAggregatorSource.EnsureRegisteredAsync(services, Config(newKey));

        var source = await StoredSourceAsync();
        source!.IsActive.Should().BeTrue();
        source.HasKey(newKey).Should().BeTrue();
        source.HasKey(Key).Should().BeFalse();
    }

    [Fact]
    public async Task NoConfiguredKey_RegistersNothing()
    {
        await MockAggregatorSource.EnsureRegisteredAsync(Services(), Config(null));

        (await StoredSourceAsync()).Should().BeNull();
    }
}