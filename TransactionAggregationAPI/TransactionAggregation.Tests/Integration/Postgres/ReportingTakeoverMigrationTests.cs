using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    [Collection(PostgresCollection.Name)]
    public class ReportingTakeoverMigrationTests(PostgresContainerFixture fixture)
    {
        private static readonly string[] Takeover = ["AggregationCheckpoints", "DailyTotals"];

        private async Task<string> LedgerDatabaseWithTotalsAsync()
        {
            var database = await fixture.CreateIsolatedDatabaseAsync(withReporting: false);
            await ExecuteAsync(database, """
                INSERT INTO transactions."DailyTotals"
                    ("SourceName", "ExternalAccountId", "Day", "Category", "Currency", "Income", "Expenses", "IncomeCount", "ExpenseCount")
                VALUES ('FNB', 'acc-1', '2026-01-05', 1, 'ZAR', 100, 40, 1, 2),
                       ('Absa', 'acc-2', '2026-01-06', 2, 'USD', 0, 9, 0, 1);
                INSERT INTO transactions."AggregationCheckpoints" ("Id", "Watermark", "AsOf") VALUES (1, now(), now());
                """);
            return database;
        }

        [Fact]
        public async Task Migrating_MovesTheTotalsAndTheirCheckpoint_WithEveryRowAndIndex()
        {
            var database = await LedgerDatabaseWithTotalsAsync();

            using var reporting = fixture.CreateReportingContext(database);
            await reporting.Database.MigrateAsync();

            (await CountAsync(database, "reporting", "DailyTotals")).Should().Be(2, "no row is lost or rebuilt");
            (await CountAsync(database, "reporting", "AggregationCheckpoints")).Should().Be(1, "the refresh checkpoint travels with the totals");
            (await TablesAsync(database, "transactions")).Should().NotIntersectWith(Takeover, "the Transactions module no longer holds them");
            (await TablesAsync(database, "reporting")).Should().BeEquivalentTo(Takeover);
            (await IndexesAsync(database, "reporting", "DailyTotals")).Should().Contain(["PK_DailyTotals", "IX_DailyTotals_Currency_Day"]);
        }

        [Fact]
        public async Task Migrating_BackAgain_PutsTheTotalsWhereTheyWere()
        {
            var database = await LedgerDatabaseWithTotalsAsync();
            using var reporting = fixture.CreateReportingContext(database);
            await reporting.Database.MigrateAsync();

            await reporting.GetService<IMigrator>().MigrateAsync("0");

            (await CountAsync(database, "transactions", "DailyTotals")).Should().Be(2);
            (await CountAsync(database, "transactions", "AggregationCheckpoints")).Should().Be(1);
            (await TablesAsync(database, "reporting")).Should().BeEmpty();
        }

        [Fact]
        public async Task Migrating_BeforeTheTransactionsMigrationsHaveRun_FailsLoudlyInsteadOfCreatingTablesInTheWrongPlace()
        {
            var name = $"empty_{Guid.NewGuid():N}";
            await using (var admin = new NpgsqlConnection(fixture.ConnectionString))
            {
                await admin.OpenAsync();
                await new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin).ExecuteNonQueryAsync();
            }

            var database = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Database = name }.ConnectionString;
            using var reporting = fixture.CreateReportingContext(database);

            var migrate = () => reporting.Database.MigrateAsync();

            (await migrate.Should().ThrowAsync<PostgresException>())
                .Which.MessageText.Should().Contain("Transactions migrations must run before the Reporting ones");
        }

        private static async Task ExecuteAsync(string connectionString, string sql)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await new NpgsqlCommand(sql, connection).ExecuteNonQueryAsync();
        }

        private static async Task<long> CountAsync(string connectionString, string schema, string table)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            return (long)(await new NpgsqlCommand($"SELECT count(*) FROM {schema}.\"{table}\"", connection).ExecuteScalarAsync())!;
        }

        private static Task<List<string>> TablesAsync(string connectionString, string schema) =>
            ReadAsync(connectionString, $"SELECT tablename FROM pg_tables WHERE schemaname = '{schema}' AND tablename IN ('DailyTotals', 'AggregationCheckpoints') ORDER BY 1");

        private static Task<List<string>> IndexesAsync(string connectionString, string schema, string table) =>
            ReadAsync(connectionString, $"SELECT indexname FROM pg_indexes WHERE schemaname = '{schema}' AND tablename = '{table}' ORDER BY 1");

        private static async Task<List<string>> ReadAsync(string connectionString, string sql)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var reader = await new NpgsqlCommand(sql, connection).ExecuteReaderAsync();
            var rows = new List<string>();
            while (await reader.ReadAsync())
                rows.Add(reader.GetString(0));
            return rows;
        }
    }
}
