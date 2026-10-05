using FluentAssertions;
using Npgsql;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    [Collection(PostgresCollection.Name)]
    public class BackupRestoreTests
    {
        private readonly PostgresContainerFixture _fixture;

        public BackupRestoreTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task DocumentedDumpAndRestore_ReproducesDataSchemaAndProtections()
        {
            var source = await _fixture.CreateIsolatedDatabaseAsync();
            var sourceName = new NpgsqlConnectionStringBuilder(source).Database!;
            await ExecuteAsync(source, """
                INSERT INTO transactions."Transactions"
                    ("Id","ExternalAccountId","Amount","Currency","Description","Category","SourceName",
                     "SourceExternalId","Status","Date","CreatedAt","UpdatedAt","Metadata")
                SELECT gen_random_uuid(), 'acc-' || (n % 7), -(n + 0.1234), 'ZAR', 'Backup ' || n, n % 12, 'FNB',
                       'bk-' || n, 4, now(), now(), NULL, jsonb_build_object('n', n)
                FROM generate_series(1, 1000) AS n;
                INSERT INTO audit."AuditEvents" ("Id","EventType","OccurredAt","RecordedAt","Channel","SourceName","Metadata")
                VALUES (gen_random_uuid(), 'inbound.received', now(), now(), 'webhook', 'backup-test', '{}');
                """);

            var restoredName = $"restored_{Guid.NewGuid():N}";
            await ExecuteAsync(_fixture.ConnectionString, $"CREATE DATABASE \"{restoredName}\"");
            await _fixture.ExecInContainerAsync("pg_dump", "-U", "postgres", "-Fc", "-f", "/tmp/backup.dump", sourceName);
            await _fixture.ExecInContainerAsync("pg_restore", "-U", "postgres", "-d", restoredName, "--clean", "--if-exists", "/tmp/backup.dump");
            var restored = new NpgsqlConnectionStringBuilder(source) { Database = restoredName }.ConnectionString;

            const string fingerprint = """SELECT count(*) || ':' || sum("Amount") FROM transactions."Transactions" """;
            (await ScalarAsync(restored, fingerprint)).Should().Be(await ScalarAsync(source, fingerprint));
            (await ScalarAsync(restored, "SELECT count(*) FROM public.\"__EFMigrationsHistory\""))
                .Should().Be(await ScalarAsync(source, "SELECT count(*) FROM public.\"__EFMigrationsHistory\""),
                    "the restored database must be at the same migration version, or the next deploy would re-run migrations");

            var rewriteLedger = () => ExecuteAsync(restored, "UPDATE transactions.\"Transactions\" SET \"Amount\" = 1");
            (await rewriteLedger.Should().ThrowAsync<PostgresException>("the insert-only trigger is part of the backup"))
                .Which.SqlState.Should().Be(PostgresErrorCodes.InsufficientPrivilege);

            var rewriteAudit = () => ExecuteAsync(restored, "UPDATE audit.\"AuditEvents\" SET \"Detail\" = 'tampered'");
            await rewriteAudit.Should().ThrowAsync<PostgresException>("the append-only triggers are part of the backup");
        }

        private static async Task ExecuteAsync(string connectionString, string sql)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await new NpgsqlCommand(sql, connection).ExecuteNonQueryAsync();
        }

        private static async Task<object?> ScalarAsync(string connectionString, string sql)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            return await new NpgsqlCommand(sql, connection).ExecuteScalarAsync();
        }
    }
}