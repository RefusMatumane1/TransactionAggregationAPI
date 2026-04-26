using Bogus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransactionAggregation.Application.Common.Interfaces;
using TransactionAggregation.Domain.Common.ValueObjects;
using TransactionAggregation.Domain.Entities;
using TransactionAggregation.Domain.Enums;

namespace TransactionAggregation.Infrastructure.Data
{
    public class DatabaseSeeder
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<DatabaseSeeder> _logger;
        private readonly Faker _faker;

        public DatabaseSeeder(IApplicationDbContext context, ILogger<DatabaseSeeder> logger)
        {
            _context = context;
            _logger = logger;
            _faker = new Faker();
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            if (await _context.Transactions.AnyAsync(cancellationToken))
            {
                _logger.LogInformation("Database already seeded");
                return;
            }

            _logger.LogInformation("Seeding database with realistic transaction data");

            var customers = GenerateCustomers();
            var transactions = new List<Transaction>();

            foreach (var customer in customers)
            {
                var customerTransactions = GenerateTransactionsForCustomer(customer);
                transactions.AddRange(customerTransactions);
            }

            await _context.Transactions.AddRangeAsync(transactions, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Seeded {TransactionCount} transactions for {CustomerCount} customers",
                transactions.Count,
                customers.Count);
        }

        private List<CustomerId> GenerateCustomers()
        {
            return Enumerable.Range(1, 10)
                .Select(_ => CustomerId.Create())
                .ToList();
        }

        private List<Transaction> GenerateTransactionsForCustomer(CustomerId customerId)
        {
            var transactionCount = _faker.Random.Int(50, 500);
            var transactions = new List<Transaction>();

            for (int i = 0; i < transactionCount; i++)
            {
                var amount = _faker.Random.Bool(0.7f)
                    ? -_faker.Finance.Amount(5, 500)  // Expense (70% chance)
                    : _faker.Finance.Amount(1000, 5000); // Income (30% chance)

                var transaction = Transaction.Create(
                    customerId,
                    Money.Create(amount),
                    _faker.Finance.Account(),
                    GetRandomCategory(amount > 0),
                    TransactionSource.Create(
                        GetRandomSource(),
                        _faker.Random.Guid().ToString()));

                transactions.Add(transaction);
            }

            return transactions;
        }

        private TransactionCategory GetRandomCategory(bool isIncome)
        {
            if (isIncome)
                return TransactionCategory.Income;

            var expenseCategories = new[]
            {
            TransactionCategory.Groceries,
            TransactionCategory.Transportation,
            TransactionCategory.Utilities,
            TransactionCategory.Entertainment,
            TransactionCategory.Shopping,
            TransactionCategory.Healthcare,
            TransactionCategory.Subscriptions
        };

            return _faker.PickRandom(expenseCategories);
        }

        private string GetRandomSource()
        {
            var sources = new[] { "Capitec Bank", "Absa Bank", "First National Bank", "PayPal", "Apple Pay" };
            return _faker.PickRandom(sources);
        }
    }
}
