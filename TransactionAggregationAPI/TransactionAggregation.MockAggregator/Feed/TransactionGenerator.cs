using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using TransactionAggregation.MockAggregator.Catalog;

namespace TransactionAggregation.MockAggregator.Feed
{
    public sealed class TransactionGenerator
    {
        private const double IncomeShare = 0.05;
        private const decimal DiningTipRate = 0.10m;

        private readonly FeedOptions _options;
        private readonly TimeProvider _time;
        private readonly Random _random;
        private readonly ConcurrentDictionary<string, List<GeneratedTransaction>> _awaitingPosting = new();

        public TransactionGenerator(IOptions<FeedOptions> options, TimeProvider time)
            : this(options, time, Random.Shared)
        {
        }

        public TransactionGenerator(IOptions<FeedOptions> options, TimeProvider time, Random random)
        {
            _options = options.Value;
            _time = time;
            _random = random;
        }

        public IReadOnlyList<GeneratedTransaction> NextBatch(MockAccount account)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var batch = new List<GeneratedTransaction>();

            if (_awaitingPosting.TryRemove(account.Id, out var pending))
                batch.AddRange(pending.Select(p => Post(p, now)));

            var awaiting = new List<GeneratedTransaction>();
            var count = _random.Next(0, _options.MaxTransactionsPerAccount + 1);
            for (var i = 0; i < count; i++)
            {
                var merchant = PickMerchant();
                var magnitude = Math.Round(
                    merchant.MinAmount + (decimal)_random.NextDouble() * (merchant.MaxAmount - merchant.MinAmount), 2);
                var isPending = merchant.Kind == SpendKind.CardPurchase && _random.NextDouble() < _options.PendingShare;

                var transaction = new GeneratedTransaction(
                    Id: $"{account.Id}-{Guid.NewGuid():N}",
                    Merchant: merchant,
                    Amount: merchant.Kind == SpendKind.Income ? magnitude : -magnitude,
                    DateUtc: now.AddMinutes(-_random.Next(0, 120)),
                    IsPending: isPending);

                batch.Add(transaction);
                if (isPending)
                    awaiting.Add(transaction);
            }

            if (awaiting.Count > 0)
                _awaitingPosting[account.Id] = awaiting;

            return batch;
        }

        private static GeneratedTransaction Post(GeneratedTransaction pending, DateTime postedAt)
        {
            var amount = pending.Merchant.Category == "Dining"
                ? Math.Round(pending.Amount * (1 + DiningTipRate), 2)
                : pending.Amount;

            return pending with { Amount = amount, DateUtc = postedAt, IsPending = false };
        }

        private Merchant PickMerchant()
        {
            var wantIncome = _random.NextDouble() < IncomeShare;
            var candidates = MockCatalog.Merchants.Where(m => (m.Kind == SpendKind.Income) == wantIncome).ToList();
            return candidates[_random.Next(candidates.Count)];
        }
    }
}