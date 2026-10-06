using Microsoft.EntityFrameworkCore;
using Modules.Transactions.Application.Common.Aggregation;
using Modules.Transactions.Domain.Enums;

namespace Modules.Transactions.Infrastructure.Persistence
{
    // Rebuilds the touched account-days (entries recorded since the checkpoint, minus the overlap for late commits)
    // in full from the ledger and moves the checkpoint, so a re-run or overlapping window is harmless.
    internal sealed class PostgresDailyTotalsRefresher(TransactionsDbContext context, TimeProvider time) : IDailyTotalsRefresher
    {
        // pg_try_advisory_xact_lock key: one refresher across all replicas.
        internal const long LockKey = 0x5441_4747_5245_4731; // "TAGGREG1"

        private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(10);

        // Before any ledger row: the first refresh rebuilds everything.
        private static readonly DateTime Beginning = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public async Task<DailyTotalsRefresh?> RefreshAsync(TimeSpan overlap, CancellationToken cancellationToken)
        {
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
                var since = watermark is { } mark ? mark - overlap : Beginning;

                var recomputed = await RecomputeTouchedAccountDaysAsync(since, ct);
                await SaveCheckpointAsync(startedAt, ct);

                await transaction.CommitAsync(ct);
                return new DailyTotalsRefresh(recomputed, startedAt);
            }, cancellationToken);
        }

        private async Task<bool> TryLockAsync(CancellationToken cancellationToken)
        {
            var locked = await context.Database
                .SqlQuery<bool>($"SELECT pg_try_advisory_xact_lock({LockKey}) AS \"Value\"")
                .SingleAsync(cancellationToken);
            return locked;
        }

        private async Task<int> RecomputeTouchedAccountDaysAsync(DateTime since, CancellationToken cancellationToken)
        {
            var booked = (int)TransactionStatus.Booked;
            var offset = (int)SouthAfricanCalendar.UtcOffsetHours;

            await context.Database.ExecuteSqlAsync($"""
                CREATE TEMP TABLE touched_account_days ON COMMIT DROP AS
                SELECT DISTINCT "SourceName", "ExternalAccountId",
                       (("Date" AT TIME ZONE 'UTC') + make_interval(hours => {offset}))::date AS "Day"
                FROM transactions."Transactions"
                WHERE "Status" = {booked} AND "CreatedAt" > {since};
                """, cancellationToken);

            // CREATE TABLE AS reports no row count, so the account-days are counted here.
            var touched = await context.Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM touched_account_days")
                .SingleAsync(cancellationToken);

            await context.Database.ExecuteSqlAsync($"""
                DELETE FROM transactions."DailyTotals" d
                USING touched_account_days t
                WHERE d."SourceName" = t."SourceName"
                  AND d."ExternalAccountId" = t."ExternalAccountId"
                  AND d."Day" = t."Day";
                """, cancellationToken);

            // Ranges on "Date" let the ledger's account index serve them.
            await context.Database.ExecuteSqlAsync($"""
                INSERT INTO transactions."DailyTotals"
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
                INSERT INTO transactions."AggregationCheckpoints" ("Id", "Watermark", "AsOf")
                VALUES ({AggregationCheckpoint.SingletonId}, {startedAt}, {startedAt})
                ON CONFLICT ("Id") DO UPDATE SET "Watermark" = EXCLUDED."Watermark", "AsOf" = EXCLUDED."AsOf";
                """, cancellationToken);
    }
}