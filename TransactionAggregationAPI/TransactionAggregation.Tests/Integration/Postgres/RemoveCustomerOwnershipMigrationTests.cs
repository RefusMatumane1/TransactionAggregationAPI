using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Modules.Transactions.Infrastructure.Migrations;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    [Collection(PostgresCollection.Name)]
    public class RemoveCustomerOwnershipMigrationTests(PostgresContainerFixture fixture)
    {
        [Fact]
        public async Task Migration_BackfillsAccountIds_MergesJointCopies_AndDropsTheOwnerSchemas()
        {
            var database = await fixture.CreateIsolatedDatabaseAsync();
            using var context = fixture.CreateContext(connectionString: database);
            var migrations = context.Database.GetMigrations().ToList();
            var removal = migrations.Single(m => m.EndsWith($"_{nameof(RemoveCustomerOwnership)}"));
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(migrations[migrations.IndexOf(removal) - 1]);

            // The two schemas as they stood: a bank link per holder of the joint account.
            var aliceAccount = Guid.NewGuid();
            var bobAccount = Guid.NewGuid();
            await context.Database.ExecuteSqlRawAsync("""
                CREATE SCHEMA customers;
                CREATE TABLE customers."Customers" ("Id" uuid PRIMARY KEY);
                CREATE SCHEMA banklinks;
                CREATE TABLE banklinks."BankLinks" ("AccountId" uuid, "ExternalAccountId" text);
                """);
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""INSERT INTO banklinks."BankLinks" VALUES ({aliceAccount}, 'ext-joint'), ({bobAccount}, 'ext-joint')""");

            var aliceCopy = Guid.NewGuid();
            await LegacyTransactionRows.InsertAsync(context, aliceCopy, "txn-1", -50m, status: 4, accountId: aliceAccount);
            await LegacyTransactionRows.InsertAsync(context, Guid.NewGuid(), "txn-1", -50m, status: 4, accountId: bobAccount);
            await LegacyTransactionRows.InsertAsync(context, Guid.NewGuid(), "txn-2", -20m, status: 4, accountId: bobAccount);
            await LegacyTransactionRows.InsertAsync(context, Guid.NewGuid(), "seed-1", -5m, status: 4);

            await migrator.MigrateAsync(removal);

            var rows = await context.Database.SqlQuery<LegacyRow>($"""
                SELECT "Id", "SourceExternalId", "ExternalAccountId", "Provider" FROM transactions."Transactions"
                """).ToListAsync();
            rows.Select(r => (r.SourceExternalId, r.ExternalAccountId)).Should().BeEquivalentTo(
            [
                ("txn-1", "ext-joint"),
                ("txn-2", "ext-joint"),
                ("seed-1", RemoveCustomerOwnership.LegacyValue)
            ], "each holder's copy of a joint transaction collapses into one row for the shared account");
            rows.Single(r => r.SourceExternalId == "txn-1").Id.Should().Be(aliceCopy, "the earliest copy is the one kept");
            rows.Should().OnlyContain(r => r.Provider == RemoveCustomerOwnership.LegacyValue);

            var remainingSchemas = await context.Database.SqlQuery<int>($"""
                SELECT count(*)::int AS "Value" FROM pg_namespace WHERE nspname IN ('customers', 'banklinks')
                """).SingleAsync();
            remainingSchemas.Should().Be(0);
        }

        private sealed record LegacyRow(Guid Id, string SourceExternalId, string ExternalAccountId, string Provider);
    }
}