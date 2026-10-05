using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Infrastructure.Persistence;

namespace TransactionAggregationAPI.Development
{
    public static class SeedData
    {
        public const string RandomSeedKey = "Seed:RandomSeed";

        // Tops every demo account up to "now": a first run backfills HistoryMonths of history, later
        // runs add only what happened since the account's latest seeded transaction.
        public static async Task SeedDatabaseAsync(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            using var scope = serviceProvider.CreateScope();
            var transactionsContext = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
            var time = scope.ServiceProvider.GetService<TimeProvider>() ?? TimeProvider.System;
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            var accounts = SeedCatalog.Households
                .SelectMany(h => h.Accounts.Select(a => new SeedAccount(a.ExternalAccountId, a.Institution, a.Type, a.VariablePerMonth, h.Profile)))
                .ToList();

            var randomSeed = configuration.GetValue<int?>(RandomSeedKey) ?? Random.Shared.Next();
            var runId = Guid.NewGuid();
            var now = time.GetUtcNow().UtcDateTime;
            var generator = new SeedTransactionGenerator(new Random(randomSeed), runId, now);

            var transactions = new List<Transaction>();
            var backfilled = 0;
            foreach (var account in accounts)
            {
                var lastSeeded = await transactionsContext.Transactions
                    .Where(t => t.Source.Name == account.Institution && t.ExternalAccountId == account.ExternalAccountId)
                    .MaxAsync(t => (DateTime?)t.Date);

                if (lastSeeded is null)
                    backfilled++;

                transactions.AddRange(generator.Generate(account, lastSeeded));
            }

            transactionsContext.Transactions.AddRange(transactions);
            await transactionsContext.SaveChangesAsync();

            logger.LogInformation(
                "Seed run {RunId} (random seed {RandomSeed}): {Transactions} transactions across {Accounts} demo accounts; {Backfilled} received {HistoryMonths} months of history",
                runId, randomSeed, transactions.Count, accounts.Count, backfilled, SeedTransactionGenerator.HistoryMonths);
        }
    }
}