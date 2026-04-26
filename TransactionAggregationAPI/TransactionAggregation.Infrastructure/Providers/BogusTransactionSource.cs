using Bogus;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Common.DTOs;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Application.Common.Models;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Infrastructure.Providers
{
    public class BogusTransactionSource : ITransactionSource
    {
        private readonly Faker<ExternalTransaction> _transactionFaker;
        private readonly ILogger<BogusTransactionSource> _logger;

        public string SourceName => "BogusGenerator";

        public BogusTransactionSource(ILogger<BogusTransactionSource> logger)
        {
            _logger = logger;

            // Initialize Bogus faker with realistic data
            _transactionFaker = new Faker<ExternalTransaction>()
                .RuleFor(t => t.Id, f => f.Random.Guid().ToString())
                .RuleFor(t => t.Amount, f => f.Finance.Amount(-1000, 5000))
                .RuleFor(t => t.Currency, f => f.Finance.Currency().Code)
               // .RuleFor(t => t.Description, f => f.Financ())
                .RuleFor(t => t.Date, f => f.Date.Past(90))
                .RuleFor(t => t.Category, f => f.PickRandom<TransactionCategory>().ToString())
                .RuleFor(t => t.MerchantName, f => f.Company.CompanyName())
                .RuleFor(t => t.Location, f => f.Address.City())
                .RuleFor(t => t.PaymentMethod, f => f.PickRandom(new[] { "Credit Card", "Debit Card", "Bank Transfer", "Mobile Payment" }));
        }

        public async Task<IReadOnlyList<ExternalTransaction>> GetTransactionsAsync(
            CustomerId customerId,
            DateTime fromDate,
            DateTime toDate,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Random.Shared.Next(50, 200), cancellationToken);

            var count = Random.Shared.Next(10, 50);
            var transactions = _transactionFaker
                .Generate(count)
                .Where(t => t.Date >= fromDate && t.Date <= toDate)
                .ToList();

            _logger.LogDebug(
                "Generated {Count} mock transactions for customer {CustomerId}",
                transactions.Count,
                customerId.Value);

            return transactions;
        }

        Task<IReadOnlyList<ExternalTransactionDTO>> ITransactionSource.GetTransactionsAsync(CustomerId customerId, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
