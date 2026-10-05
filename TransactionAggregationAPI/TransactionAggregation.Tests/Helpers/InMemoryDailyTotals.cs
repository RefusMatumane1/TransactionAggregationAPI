using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Domain.Services;
using Modules.Transactions.Infrastructure.Persistence;

namespace TransactionAggregation.Tests.Helpers
{
    // Builds the daily read model from an in-memory ledger the way PostgresDailyTotalsRefresher does
    // in SQL (whose own behaviour the Postgres tests cover), so the aggregate query handlers can be
    // tested without a database server.
    public static class InMemoryDailyTotals
    {
        public static async Task BuildAsync(TransactionsDbContext context, DateTime? asOf = null)
        {
            context.DailyTotals.RemoveRange(await context.DailyTotals.ToListAsync());
            context.AggregationCheckpoints.RemoveRange(await context.AggregationCheckpoints.ToListAsync());

            var ledger = await context.Transactions.Where(LedgerEntries.IsEntry).ToListAsync();
            context.DailyTotals.AddRange(ledger
                .GroupBy(t => (Day: SouthAfricanCalendar.DayOf(t.Date), t.Source.Name, t.ExternalAccountId, t.Category, t.Amount.Currency))
                .Select(g => new DailyTotal
                {
                    Day = g.Key.Day,
                    SourceName = g.Key.Name,
                    ExternalAccountId = g.Key.ExternalAccountId,
                    Category = g.Key.Category,
                    Currency = g.Key.Currency,
                    Income = g.Where(t => t.Amount.Amount > 0).Sum(t => t.Amount.Amount),
                    Expenses = -g.Where(t => t.Amount.Amount < 0).Sum(t => t.Amount.Amount),
                    IncomeCount = g.Count(t => t.Amount.Amount > 0),
                    ExpenseCount = g.Count(t => t.Amount.Amount < 0)
                }));

            var at = asOf ?? DateTime.UtcNow;
            context.AggregationCheckpoints.Add(new AggregationCheckpoint { Id = AggregationCheckpoint.SingletonId, Watermark = at, AsOf = at });

            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }
    }
}