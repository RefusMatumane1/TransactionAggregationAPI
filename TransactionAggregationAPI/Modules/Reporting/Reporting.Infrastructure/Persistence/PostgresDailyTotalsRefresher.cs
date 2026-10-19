using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Modules.Reporting.Application.Aggregation;
using Modules.Transactions.Contracts;

namespace Modules.Reporting.Infrastructure.Persistence
{
    /// <summary>
    /// Rebuilds the daily totals from the Transactions module's ledger table. That read crosses the module boundary on
    /// purpose and in one direction only: the ledger (<c>transactions."Transactions"</c>, booked rows) is the source,
    /// this module's own tables are the only thing written. DailyTotalsRefresherPostgresTests pins the columns relied on.
    /// </summary>
    internal sealed class PostgresDailyTotalsRefresher(ReportingDbContext context, TimeProvider time) : IDailyTotalsRefresher
    {
        internal const long LockKey = 0x5441_4747_5245_4731;

        private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(10);

        private static readonly DateTime Beginning = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public async Task<DailyTotalsRefresh?> RefreshAsync(TimeSpan overlap, int maxRows, CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxRows, 1);
            context.Database.SetCommandTimeout(CommandTimeout);
            var strategy = context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async ct =>
            {
                var startedAt = time.GetUtcNow().UtcDateTime;

                await using var transaction = await context.Database.BeginTransactionAsync(ct);

                if (!await TryLockAsync(ct))
                    return null;

                var watermark = await context.AggregationCheckpoints
                    .Where(c => c.Id == AggregationCheckpoint.SingletonId)
                    .Select(c => (DateTime?)c.Watermark)
                    .FirstOrDefaultAsync(ct);
                var mark = watermark ?? Beginning;
                var since = mark - overlap;

                var cutoff = await FindBatchCutoffAsync(mark, maxRows, ct);
                var until = cutoff ?? startedAt;

                var recomputed = await RecomputeTouchedAccountDaysAsync(since, until, ct);
                await SaveCheckpointAsync(until, ct);

                await transaction.CommitAsync(ct);
                return new DailyTotalsRefresh(recomputed, until, HasMore: cutoff is not null);
            }, cancellationToken);
        }

        private async Task<DateTime?> FindBatchCutoffAsync(DateTime mark, int maxRows, CancellationToken cancellationToken)
        {
            if (maxRows == int.MaxValue)
                return null;

            var booked = (int)TransactionStatus.Booked;
            var cutoff = await context.Database
                .SqlQuery<DateTime>($"""
                    SELECT "CreatedAt" AS "Value"
                    FROM transactions."Transactions"
                    WHERE "Status" = {booked} AND "CreatedAt" > {mark}
                    ORDER BY "CreatedAt"
                    OFFSET {maxRows - 1} LIMIT 1
                    """)
                .ToListAsync(cancellationToken);
            return cutoff.Count == 0 ? null : cutoff[0];
        }

        private async Task<bool> TryLockAsync(CancellationToken cancellationToken)
        {
            var locked = await context.Database
                .SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock({LockKey}) AS \"Value\"")
                .SingleAsync(cancellationToken);
            return locked;
        }

        private async Task<int> RecomputeTouchedAccountDaysAsync(DateTime since, DateTime until, CancellationToken cancellationToken)
        {
            var booked = (int)TransactionStatus.Booked;
            var offset = (int)SouthAfricanCalendar.UtcOffsetHours;

            await context.Database.ExecuteSqlAsync($"""
                CREATE TEMP TABLE touched_account_days ON COMMIT DROP AS
                SELECT DISTINCT "SourceName", "ExternalAccountId",
                       (("Date" AT TIME ZONE 'UTC') + make_interval(hours => {offset}))::date AS "Day"
                FROM transactions."Transactions"
                WHERE "Status" = {booked} AND "CreatedAt" > {since} AND "CreatedAt" <= {until};
                """, cancellationToken);

            var touched = await context.Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM touched_account_days")
                .SingleAsync(cancellationToken);

            await context.Database.ExecuteSqlAsync($"""
                DELETE FROM reporting."DailyTotals" d
                USING touched_account_days t
                WHERE d."SourceName" = t."SourceName"
                  AND d."ExternalAccountId" = t."ExternalAccountId"
                  AND d."Day" = t."Day";
                """, cancellationToken);

            await context.Database.ExecuteSqlAsync($"""
                INSERT INTO reporting."DailyTotals"
                    ("SourceName", "ExternalAccountId", "Day", "Category", "Currency",
                     "Income", "Expenses", "IncomeCount", "ExpenseCount")
                SELECT t."SourceName", t."ExternalAccountId", t."Day", x."Category", x."Currency",
                       COALESCE(SUM(x."Amount") FILTER (WHERE x."Amount" > 0), 0),
                       COALESCE(-SUM(x."Amount") FILTER (WHERE x."Amount" < 0), 0),
                       COUNT(*) FILTER (WHERE x."Amount" > 0),
                       COUNT(*) FILTER (WHERE x."Amount" < 0)
                FROM touched_account_days t
                JOIN transactions."Transactions" x
                  ON x."SourceName" = t."SourceName"
                 AND x."ExternalAccountId" = t."ExternalAccountId"
                 AND x."Date" >= (t."Day"::timestamp - make_interval(hours => {offset})) AT TIME ZONE 'UTC'
                 AND x."Date" < (t."Day"::timestamp + interval '1 day' - make_interval(hours => {offset})) AT TIME ZONE 'UTC'
                WHERE x."Status" = {booked}
                GROUP BY t."SourceName", t."ExternalAccountId", t."Day", x."Category", x."Currency";
                """, cancellationToken);

            return touched;
        }

        private Task SaveCheckpointAsync(DateTime startedAt, CancellationToken cancellationToken) =>
            context.Database.ExecuteSqlAsync($"""
                INSERT INTO reporting."AggregationCheckpoints" ("Id", "Watermark", "AsOf")
                VALUES ({AggregationCheckpoint.SingletonId}, {startedAt}, {startedAt})
                ON CONFLICT ("Id") DO UPDATE SET "Watermark" = EXCLUDED."Watermark", "AsOf" = EXCLUDED."AsOf";
                """, cancellationToken);
    }
}