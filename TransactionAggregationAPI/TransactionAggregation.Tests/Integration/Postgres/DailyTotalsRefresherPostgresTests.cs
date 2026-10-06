using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Application.Features.Transactions.Queries.Aggregates;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using Modules.Transactions.Infrastructure.Persistence;
using Npgsql;
using TransactionAggregation.Tests.Helpers;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    // A database per test: the refresh and its checkpoint are global.
    [Collection(PostgresCollection.Name)]
    public class DailyTotalsRefresherPostgresTests(PostgresContainerFixture fixture)
    {
        private const string Bank = "FNB";
        private const string Account = "acc-1";

        private static DateTime Utc(int month, int day, int hour) => new(2026, month, day, hour, 0, 0, DateTimeKind.Utc);

        private static Transaction Entry(decimal amount, DateTime bookedAt, DateTime? recordedAt = null, TransactionCategory category = TransactionCategory.Groceries) =>
            Transaction.Record(Account, Money.Create(amount, "ZAR"), "Refresh test", category,
                TransactionSource.Create(Bank, Guid.NewGuid().ToString("N")), bookedAt, recordedAt: recordedAt);

        private async Task RecordAsync(string db, params Transaction[] entries)
        {
            using var context = fixture.CreateContext(connectionString: db);
            context.Transactions.AddRange(entries);
            await context.SaveChangesAsync();
        }

        private async Task<List<DailyTotal>> TotalsAsync(string db)
        {
            using var context = fixture.CreateContext(connectionString: db);
            return await context.DailyTotals.AsNoTracking().OrderBy(d => d.Day).ThenBy(d => d.Category).ToListAsync();
        }

        [Fact]
        public async Task FirstRefresh_BuildsEveryDay_FromBookedEntries_InSouthAfricanDays()
        {
            var db = await fixture.CreateIsolatedDatabaseAsync();
            await RecordAsync(db,
                Entry(-100m, Utc(3, 1, 9)),
                Entry(-40m, Utc(3, 1, 22)),
                Entry(500m, Utc(3, 1, 10), category: TransactionCategory.Income));
            using (var context = fixture.CreateContext(connectionString: db))
                await PreLedgerRows.InsertAsync(context, Bank, Account, "pending-1", -999m, TransactionStatus.Pending, Utc(3, 1, 9));

            var refresh = await fixture.RefreshDailyTotalsAsync(db);

            refresh.Should().NotBeNull();
            (await TotalsAsync(db)).Select(d => (d.Day, d.Category, d.Income, d.Expenses, d.IncomeCount, d.ExpenseCount)).Should().Equal(
                (new DateOnly(2026, 3, 1), TransactionCategory.Groceries, 0m, 100m, 0, 1),
                (new DateOnly(2026, 3, 1), TransactionCategory.Income, 500m, 0m, 1, 0),
                (new DateOnly(2026, 3, 2), TransactionCategory.Groceries, 0m, 40m, 0, 1));
        }

        [Fact]
        public async Task ABackdatedEntry_RecomputesTheDayItWasBooked_AndARerunChangesNothing()
        {
            var db = await fixture.CreateIsolatedDatabaseAsync();
            await RecordAsync(db, Entry(-100m, Utc(1, 15, 9)));
            await fixture.RefreshDailyTotalsAsync(db);

            await RecordAsync(db, Entry(-25m, Utc(1, 15, 12)));
            var refresh = await fixture.RefreshDailyTotalsAsync(db);
            var rerun = await fixture.RefreshDailyTotalsAsync(db);

            refresh!.AccountDaysRecomputed.Should().Be(1, "only the day the new entry was booked on is rebuilt");
            rerun.Should().NotBeNull();
            (await TotalsAsync(db)).Should().ContainSingle()
                .Which.Should().Match<DailyTotal>(d => d.Expenses == 125m && d.ExpenseCount == 2,
                    "the day is recomputed in full, never added to, so a re-run cannot double count");
        }

        [Fact]
        public async Task AnEntryCommittedLate_WithinTheOverlap_IsStillFoldedIn()
        {
            var db = await fixture.CreateIsolatedDatabaseAsync();
            var first = await fixture.RefreshDailyTotalsAsync(db);

            // Recorded before the checkpoint but visible only after it: a slow commit.
            await RecordAsync(db, Entry(-60m, Utc(2, 3, 9), recordedAt: first!.AsOf.AddMinutes(-3)));
            await fixture.RefreshDailyTotalsAsync(db, overlap: TimeSpan.FromMinutes(10));

            (await TotalsAsync(db)).Should().ContainSingle().Which.Expenses.Should().Be(60m);
        }

        [Fact]
        public async Task WhileAnotherReplicaRefreshes_ARefreshSkips()
        {
            var db = await fixture.CreateIsolatedDatabaseAsync();
            await RecordAsync(db, Entry(-10m, Utc(4, 1, 9)));

            await using var otherReplica = new NpgsqlConnection(db);
            await otherReplica.OpenAsync();
            await using var holding = await otherReplica.BeginTransactionAsync();
            await using (var take = new NpgsqlCommand($"SELECT pg_advisory_xact_lock({PostgresDailyTotalsRefresher.LockKey})", otherReplica, holding))
                await take.ExecuteNonQueryAsync();

            var skipped = await fixture.RefreshDailyTotalsAsync(db);
            await holding.RollbackAsync();
            var ran = await fixture.RefreshDailyTotalsAsync(db);

            skipped.Should().BeNull();
            ran.Should().NotBeNull();
            (await TotalsAsync(db)).Should().ContainSingle();
        }

        [Fact]
        public async Task Aggregates_ReadTheReadModel_AndSayHowOldItIs()
        {
            var db = await fixture.CreateIsolatedDatabaseAsync();
            await RecordAsync(db, Entry(-80m, Utc(5, 10, 9)));
            var refresh = await fixture.RefreshDailyTotalsAsync(db);
            await RecordAsync(db, Entry(-1000m, Utc(5, 11, 9)));

            using var context = fixture.CreateContext(connectionString: db);
            var cashFlow = (await new GetCashFlowQueryHandler(context).Handle(
                new GetCashFlowQuery(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31), TestFilters.For(Bank)),
                CancellationToken.None)).Value;

            cashFlow.TotalExpenses.Should().Be(80m, "an entry recorded after the last refresh waits for the next one");
            cashFlow.AsOf.Should().BeCloseTo(refresh!.AsOf, TimeSpan.FromMilliseconds(1));
        }
    }
}