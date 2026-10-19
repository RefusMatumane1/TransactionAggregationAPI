using Microsoft.EntityFrameworkCore;
using Modules.Reporting.Application.Aggregation;
using Modules.Reporting.Infrastructure.Persistence;
using Modules.Transactions.Domain.Services;
using Modules.Transactions.Infrastructure.Persistence;
using System.Runtime.CompilerServices;

namespace TransactionAggregation.Tests.Helpers
{
    /// <summary>
    /// Stands in for the totals refresher in in-memory tests: builds the Reporting module's daily totals from a ledger
    /// context, and hands the matching reporting context to the handlers under test.
    /// </summary>
    public static class InMemoryDailyTotals
    {
        private static readonly ConditionalWeakTable<TransactionsDbContext, ReportingDbContext> Reports = new();

        public static ReportingDbContext Of(TransactionsDbContext ledger) =>
            Reports.TryGetValue(ledger, out var reporting)
                ? reporting
                : throw new InvalidOperationException("Build the daily totals for this ledger first: InMemoryDailyTotals.BuildAsync.");

        /// <summary>Builds the totals into a reporting context of its own, found again with <see cref="Of"/>.</summary>
        public static Task BuildAsync(TransactionsDbContext ledger, DateTime? asOf = null) =>
            BuildAsync(ledger, Reports.GetValue(ledger, _ => InMemoryDb.Create<ReportingDbContext>()), asOf);

        /// <summary>Builds the totals into <paramref name="reporting"/>, e.g. the one a test host resolves for its requests.</summary>
        public static async Task BuildAsync(TransactionsDbContext ledger, ReportingDbContext reporting, DateTime? asOf = null)
        {
            reporting.DailyTotals.RemoveRange(await reporting.DailyTotals.ToListAsync());
            reporting.AggregationCheckpoints.RemoveRange(await reporting.AggregationCheckpoints.ToListAsync());

            var entries = await ledger.Transactions.Where(LedgerEntries.IsEntry).ToListAsync();
            reporting.DailyTotals.AddRange(entries
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
            reporting.AggregationCheckpoints.Add(new AggregationCheckpoint { Id = AggregationCheckpoint.SingletonId, Watermark = at, AsOf = at });

            await reporting.SaveChangesAsync();
            reporting.ChangeTracker.Clear();
        }
    }
}
