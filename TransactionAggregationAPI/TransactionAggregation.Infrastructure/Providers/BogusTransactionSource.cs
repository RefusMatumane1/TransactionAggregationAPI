using Bogus;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Domain.Common.ValueObjects;

namespace TransactionAggregation.Infrastructure.Providers
{
    public class BogusTransactionSource : ITransactionSource
    {
        private static readonly string[] Descriptions =
        {
            "grocery store", "netflix subscription", "uber ride", "electric bill",
            "amazon purchase", "restaurant dining", "starbucks coffee", "spotify subscription",
            "water bill", "supermarket", "internet service", "walmart grocery",
            "lyft ride", "cinema tickets", "rent payment", "bus pass"
        };

        private readonly Faker _faker;
        private readonly ILogger<BogusTransactionSource> _logger;

        public string SourceName => "BogusBank";

        public BogusTransactionSource(ILogger<BogusTransactionSource> logger)
        {
            _logger = logger;
            _faker = new Faker();
        }

        public async Task<IReadOnlyList<ExternalTransactionDTO>> GetTransactionsAsync(
            CustomerId customerId,
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            // Simulate async latency from an external source
            await Task.Delay(_faker.Random.Int(50, 200), cancellationToken);

            var count = _faker.Random.Int(8, 20);
            var transactions = Enumerable.Range(0, count)
                .Select(i => new ExternalTransactionDTO
                {
                    Id = $"bogus-{customerId.Value:N}-{i:D3}",
                    Amount = _faker.Finance.Amount(-1000, 5000),
                    Currency = "ZAR",
                    Description = _faker.PickRandom(Descriptions),
                    Category = "",
                    Date = _faker.Date.Between(fromDate, toDate)
                })
                .ToList();

            _logger.LogDebug(
                "BogusBank generated {Count} transactions for customer {CustomerId}",
                transactions.Count, customerId.Value);

            return transactions;
        }
    }
}
