using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Enums;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    [Collection(PostgresCollection.Name)]
    public class BackfillSettledStatusMigrationTests
    {
        private const string BackfillMigration = "BackfillSettledStatusForIngestedTransactions";

        private readonly PostgresContainerFixture _fixture;

        public BackfillSettledStatusMigrationTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Backfill_SettlesPendingIngestedRows_AndLeavesSeedAndOtherStatusesAlone()
        {
            var database = await _fixture.CreateIsolatedDatabaseAsync();

            using var context = _fixture.CreateContext(connectionString: database);
            var migrations = context.Database.GetMigrations().ToList();
            var backfill = migrations.Single(m => m.EndsWith(BackfillMigration));
            var previous = migrations[migrations.IndexOf(backfill) - 1];

            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(previous);

            var suffix = Guid.NewGuid().ToString("N");
            var ingestedPending = Guid.NewGuid();
            var seedPending = Guid.NewGuid();
            var ingestedRejected = Guid.NewGuid();
            await LegacyTransactionRows.InsertAsync(context, ingestedPending, $"bank-{suffix}", -10m, status: 0);
            await LegacyTransactionRows.InsertAsync(context, seedPending, $"seed-{suffix}", -10m, status: 0);
            // 2 was "Rejected" at this point in the schema's history.
            await LegacyTransactionRows.InsertAsync(context, ingestedRejected, $"bank-rejected-{suffix}", -10m, status: 2);

            await migrator.MigrateAsync();

            using var verify = _fixture.CreateContext(connectionString: database);
            async Task<Modules.Transactions.Domain.Entities.Transaction> Reload(Guid id) =>
                await verify.Transactions.AsNoTracking().SingleAsync(x => x.Id == TransactionId.CreateFrom(id));

            (await Reload(ingestedPending)).Status.Should().Be(TransactionStatus.Booked);
            var updatedAt = await verify.Transactions.AsNoTracking()
                .Where(x => x.Id == TransactionId.CreateFrom(ingestedPending))
                .Select(x => EF.Property<DateTime?>(x, "UpdatedAt"))
                .SingleAsync();
            updatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));

            (await Reload(seedPending)).Status.Should().Be(TransactionStatus.Pending,
                "seed data is deliberately given a mix of statuses and must not be rewritten");
            (await Reload(ingestedRejected)).Status.Should().Be(TransactionStatus.Expired,
                "the backfill touches only Pending rows; the later retirement migration maps legacy Rejected to Expired");
        }
    }
}