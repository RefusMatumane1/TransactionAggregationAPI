using FluentAssertions;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TransactionAggregation.MockAggregator;
using TransactionAggregation.MockAggregator.Catalog;
using TransactionAggregation.MockAggregator.Consent;
using TransactionAggregation.MockAggregator.Feed;
using Xunit;

namespace TransactionAggregation.Tests.Unit.MockBanks;

public class TransactionGeneratorTests
{
    private static readonly MockAccount Account = MockCatalog.Accounts[0];

    private static TransactionGenerator Build(double pendingShare, int seed = 7) => new(
        Options.Create(new FeedOptions { MaxTransactionsPerAccount = 8, PendingShare = pendingShare }),
        TimeProvider.System,
        new Random(seed));

    [Fact]
    public void PendingCardPurchases_ArePostedOnTheNextBatchUnderTheSameId()
    {
        var generator = Build(pendingShare: 1.0);
        var first = Enumerable.Range(0, 20).Select(_ => generator.NextBatch(Account)).First(b => b.Any(t => t.IsPending));
        var pending = first.Where(t => t.IsPending).ToList();

        var second = generator.NextBatch(Account);

        foreach (var original in pending)
        {
            var posted = second.Single(t => t.Id == original.Id);
            posted.IsPending.Should().BeFalse();
            posted.Amount.Should().Be(original.Merchant.Category == "Dining"
                ? Math.Round(original.Amount * 1.10m, 2)   // a tip added at posting
                : original.Amount);
        }
    }

    [Fact]
    public void OnlyCardPurchasesAreEverPending_AndSignsFollowTheDirectionOfMoney()
    {
        var generator = Build(pendingShare: 1.0);
        var all = Enumerable.Range(0, 50).SelectMany(_ => generator.NextBatch(Account)).ToList();

        all.Where(t => t.IsPending).Should().OnlyContain(t => t.Merchant.Kind == SpendKind.CardPurchase);
        all.Where(t => t.Merchant.Kind == SpendKind.Income).Should().OnlyContain(t => t.Amount > 0);
        all.Where(t => t.Merchant.Kind != SpendKind.Income).Should().OnlyContain(t => t.Amount < 0);
    }
}

public class ConsentStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "consent-store-tests", Guid.NewGuid().ToString("N"));

    private ConsentStore NewStore() => new(
        Options.Create(new MockAggregatorOptions { DataDirectory = "data" }),
        new HostingEnvironment { ContentRootPath = _root },
        TimeProvider.System,
        NullLogger<ConsentStore>.Instance);

    [Fact]
    public void Consents_SurviveARestart_AndRevokingStopsThem()
    {
        NewStore().IssueTokens("mock-fnb-chq-1001");

        var afterRestart = NewStore();
        afterRestart.ConsentedAccounts().Select(a => a.Id).Should().Equal("mock-fnb-chq-1001");

        afterRestart.Revoke("mock-fnb-chq-1001").Should().BeTrue();
        NewStore().ConsentedAccounts().Should().BeEmpty();
    }

    [Fact]
    public void Code_IsBoundToTheClientAndRedirectItWasIssuedFor()
    {
        var store = NewStore();
        var code = store.IssueCode("mock-fnb-chq-1001", "client", "https://app/cb");

        store.TryRedeemCode(code, "client", "https://other/cb", out _).Should().BeFalse();
        store.TryRedeemCode(code, "client", "https://app/cb", out _).Should().BeFalse("a failed attempt still consumes the code");
    }

    [Fact]
    public void JointAccount_IsConsentedOnce_HoweverManyHoldersLinkIt()
    {
        var store = NewStore();
        store.IssueTokens("mock-fnb-joint-1003");
        store.IssueTokens("mock-fnb-joint-1003");

        store.ConsentedAccounts().Should().ContainSingle("the aggregator pushes once; the application copies to each holder");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}