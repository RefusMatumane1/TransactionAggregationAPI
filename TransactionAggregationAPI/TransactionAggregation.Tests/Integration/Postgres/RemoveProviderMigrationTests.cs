using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Modules.Transactions.Infrastructure.Migrations;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    [Collection(PostgresCollection.Name)]
    public class RemoveProviderMigrationTests(PostgresContainerFixture fixture)
    {
        [Fact]
        public async Task Migration_DropsProvider_KeepsEveryRow_AndRebuildsTheCoveringIndexWithoutIt()
        {
            var database = await fixture.CreateIsolatedDatabaseAsync();
            using var context = fixture.CreateContext(connectionString: database);
            var migrations = context.Database.GetMigrations().ToList();
            var removal = migrations.Single(m => m.EndsWith($"_{nameof(RemoveProvider)}"));
            var before = migrations[migrations.IndexOf(removal) - 1];
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(before);

            await context.Database.ExecuteSqlRawAsync("""
                INSERT INTO transactions."Transactions"
                    ("Id", "Provider", "ExternalAccountId", "Amount", "Currency", "Description", "Category", "SourceName",
                     "SourceExternalId", "Status", "Date", "CreatedAt", "UpdatedAt", "Metadata")
                SELECT gen_random_uuid(), 'mock-aggregator', 'acc-' || n, -n, 'ZAR', 'Row ' || n, 1, 'FNB',
                       'rp-' || n, 4, now(), now(), NULL, jsonb_build_object()
                FROM generate_series(1, 25) AS n;
                """);

            await migrator.MigrateAsync(removal);

            (await Scalar(context, """SELECT count(*)::int AS "Value" FROM transactions."Transactions" """)).Should().Be(25);
            (await Scalar(context, """
                SELECT count(*)::int AS "Value" FROM information_schema.columns
                WHERE table_schema = 'transactions' AND table_name = 'Transactions' AND column_name = 'Provider'
                """)).Should().Be(0);
            (await IndexDefinition(context, "IX_Transactions_Date_Id_Covering")).Should()
                .Contain("INCLUDE (\"Amount\", \"Status\", \"Category\", \"SourceName\", \"ExternalAccountId\")");
            (await IndexDefinition(context, "IX_Transactions_Date_Id_Covering_New")).Should().BeNull("the new index is renamed into place");
            (await IndexDefinition(context, "IX_Transactions_Provider")).Should().BeNull();

            await migrator.MigrateAsync(before);

            (await Scalar(context, """SELECT count(*)::int AS "Value" FROM transactions."Transactions" WHERE "Provider" = "SourceName" """))
                .Should().Be(25, "rolling back restores the column, filled with each row's bank");
            (await IndexDefinition(context, "IX_Transactions_Date_Id_Covering")).Should().Contain("\"Provider\"");
        }

        private static Task<int> Scalar(DbContext context, string sql) =>
            context.Database.SqlQueryRaw<int>(sql).SingleAsync();

        private static Task<string?> IndexDefinition(DbContext context, string name) =>
            context.Database.SqlQuery<string?>($"""
                SELECT indexdef AS "Value" FROM pg_indexes WHERE schemaname = 'transactions' AND indexname = {name}
                """).SingleOrDefaultAsync();
    }
}