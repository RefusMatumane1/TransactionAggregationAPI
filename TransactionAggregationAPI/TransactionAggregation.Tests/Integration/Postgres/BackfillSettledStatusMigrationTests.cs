using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Modules.Customers.Domain;
using Modules.Customers.Domain.ValueObjects;
using Modules.Transactions.Domain.Common.ValueObjects;
using Modules.Transactions.Domain.Entities;
using Modules.Transactions.Domain.Enums;
using SharedKernel.Common.ValueObjects;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    /// <summary>
    /// Runs the real BackfillSettledStatusForIngestedTransactions migration against real rows:
    /// steps the migration history back one (its Down is a no-op), inserts rows as the old
    /// code / the seeder would have left them, then migrates forward again.
    /// </summary>
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
            // Migrating down and back up rewrites the schema, so this runs in its own database:
            // rows other tests stored under today's constraints can legitimately block a
            // rollback to older ones.
            var database = await _fixture.CreateIsolatedDatabaseAsync();

            using var context = _fixture.CreateContext(connectionString: database);
            var migrations = context.Database.GetMigrations().ToList();
            var backfill = migrations.Single(m => m.EndsWith(BackfillMigration));
            var previous = migrations[migrations.IndexOf(backfill) - 1];

            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(previous);

            using var customers = _fixture.CreateCustomersContext(database);
            var customer = Customer.Create(CustomerId.Create(), $"{Guid.NewGuid()}@example.com", "Backfill Test");
            var account = Account.Create(customer.Id, $"acc-{Guid.NewGuid():N}", "Backfill", AccountType.Checking, "ZAR");
            customers.Customers.Add(customer);
            customers.Accounts.Add(account);
            await customers.SaveChangesAsync();

            Transaction Tx(string externalId, TransactionStatus status)
            {
                var tx = Transaction.Create(customer.Id, Money.Create(-10m, "ZAR"), "backfill", TransactionCategory.Uncategorized,
                    TransactionSource.Create("FNB", externalId), account.Id);
                if (status != TransactionStatus.Pending)
                    tx.UpdateStatus(status);
                return tx;
            }

            var suffix = Guid.NewGuid().ToString("N");
            var ingestedPending = Tx($"bank-{suffix}", TransactionStatus.Pending);
            var seedPending = Tx($"seed-{suffix}", TransactionStatus.Pending);
            var ingestedRejected = Tx($"bank-rejected-{suffix}", TransactionStatus.Rejected);
            using (var seedContext = _fixture.CreateContext(connectionString: database))
            {
                seedContext.Transactions.AddRange(ingestedPending, seedPending, ingestedRejected);
                await seedContext.SaveChangesAsync();
            }

            await migrator.MigrateAsync();

            using var verify = _fixture.CreateContext(connectionString: database);
            async Task<Transaction> Reload(Transaction t) =>
                await verify.Transactions.AsNoTracking().SingleAsync(x => x.Id == t.Id);

            var settled = await Reload(ingestedPending);
            settled.Status.Should().Be(TransactionStatus.Settled);
            settled.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));

            (await Reload(seedPending)).Status.Should().Be(TransactionStatus.Pending,
                "seed data is deliberately given a mix of statuses and must not be rewritten");
            (await Reload(ingestedRejected)).Status.Should().Be(TransactionStatus.Rejected,
                "only Pending rows are backfilled");
        }
    }
}