using BuildingBlocks.Application.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Modules.Currency.Contracts;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Infrastructure.Persistence;

namespace Modules.Transactions.Infrastructure.Development
{
    internal sealed class TransactionsDemoSeeder(
        TransactionsDbContext context,
        IConfiguration configuration,
        TimeProvider time,
        ICurrencyCatalog currencies,
        ILogger<TransactionsDemoSeeder> logger) : IDevelopmentSeeder
    {
        public const string RandomSeedKey = "Seed:RandomSeed";

        public async Task SeedAsync(CancellationToken cancellationToken)
        {
            var accounts = SeedCatalog.Households
                .SelectMany(h => h.Accounts.Select(a => new SeedAccount(a.ExternalAccountId, a.Institution, a.Type, a.VariablePerMonth, h.Profile)))
                .ToList();

            var randomSeed = configuration.GetValue<int?>(RandomSeedKey) ?? Random.Shared.Next();
            var runId = Guid.NewGuid();
            var generator = new SeedTransactionGenerator(new Random(randomSeed), runId, time.GetUtcNow().UtcDateTime, currencies.BaseCurrency);

            var transactions = new List<Transaction>();
            var backfilled = 0;
            foreach (var account in accounts)
            {
                var lastSeeded = await context.Transactions
                    .Where(t => t.Source.Name == account.Institution && t.ExternalAccountId == account.ExternalAccountId)
                    .MaxAsync(t => (DateTime?)t.Date, cancellationToken);

                if (lastSeeded is null)
                    backfilled++;

                transactions.AddRange(generator.Generate(account, lastSeeded));
            }

            context.Transactions.AddRange(transactions);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Seed run {RunId} (random seed {RandomSeed}): {Transactions} transactions across {Accounts} demo accounts; {Backfilled} received {HistoryMonths} months of history",
                runId, randomSeed, transactions.Count, accounts.Count, backfilled, SeedTransactionGenerator.HistoryMonths);
        }
    }
}
