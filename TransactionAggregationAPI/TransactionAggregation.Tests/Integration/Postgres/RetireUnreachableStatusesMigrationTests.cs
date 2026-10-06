using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Modules.Transactions.Domain.Enums;
using Npgsql;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    [Collection(PostgresCollection.Name)]
    public class RetireUnreachableStatusesMigrationTests(PostgresContainerFixture fixture)
    {
        private const string Migration = "_RetireUnreachableTransactionStatuses";

        [Fact]
        public async Task LegacyStatusesBecomeExpired_WithoutChangingAnyBalance_AndCanNoLongerBeWritten()
        {
            var database = await fixture.CreateIsolatedDatabaseAsync();
            using var context = fixture.CreateContext(connectionString: database);
            var migrations = context.Database.GetMigrations().ToList();
            var retire = migrations.Single(m => m.EndsWith(Migration));
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(migrations[migrations.IndexOf(retire) - 1]);

            var rows = Enumerable.Range(0, 9).Select(status => (Status: status, Id: Guid.NewGuid())).ToList();
            foreach (var (status, id) in rows)
                await LegacyTransactionRows.InsertAsync(
                    context, id, $"legacy-{status}-{Guid.NewGuid():N}", -(status + 1) * 10m, status, description: $"legacy {status}");

            var totalsBefore = await TotalsAsync(database);

            await migrator.MigrateAsync();

            (await TotalsAsync(database)).Should().Be(totalsBefore,
                "balances count only Settled and Pending, and neither changed");

            using var verify = fixture.CreateContext(connectionString: database);
            var statuses = await verify.Transactions.AsNoTracking()
                .Where(t => t.Description.StartsWith("legacy "))
                .ToDictionaryAsync(t => t.Description, t => (int)t.Status);
            foreach (var status in Enumerable.Range(0, 9))
                statuses[$"legacy {status}"].Should().Be(status is 0 or 4 or 8 ? status : 8, $"legacy status {status}");

            var writeRetired = () => PreLedgerRows.InsertAsync(
                verify, "FNB", "acc-retired", $"retired-{Guid.NewGuid():N}", -1m, (TransactionStatus)1, DateTime.UtcNow);
            (await writeRetired.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
        }

        private async Task<(decimal Booked, decimal Pending)> TotalsAsync(string database)
        {
            using var context = fixture.CreateContext(connectionString: database);
            var booked = await context.Database.SqlQuery<decimal>(
                $"SELECT COALESCE(SUM(\"Amount\"), 0) AS \"Value\" FROM transactions.\"Transactions\" WHERE \"Status\" = 4").SingleAsync();
            var pending = await context.Database.SqlQuery<decimal>(
                $"SELECT COALESCE(SUM(\"Amount\"), 0) AS \"Value\" FROM transactions.\"Transactions\" WHERE \"Status\" = 0").SingleAsync();
            return (booked, pending);
        }
    }
}