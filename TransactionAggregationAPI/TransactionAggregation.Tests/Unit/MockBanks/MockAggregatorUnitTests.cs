using FluentAssertions;
using Microsoft.Extensions.Options;
using TransactionAggregation.MockAggregator;
using TransactionAggregation.MockAggregator.Catalog;
using TransactionAggregation.MockAggregator.Feed;
using Xunit;

namespace TransactionAggregation.Tests.Unit.MockBanks
{
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
                    ? Math.Round(original.Amount * 1.10m, 2)
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
}