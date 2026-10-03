using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.RegularExpressions;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    [Collection(PostgresCollection.Name)]
    public partial class LeastPrivilegeRolesTests
    {
        private const string MigratorPassword = "migrator-test-password";
        private const string AppPassword = "app-test-password";

        private readonly PostgresContainerFixture _fixture;

        public LeastPrivilegeRolesTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ApplicationRole_CanReadAndWriteData_ButCannotChangeSchemaOrRewriteAudit()
        {
            var (asMigrator, asApp) = await ProvisionAsync();

            await ExecuteAsync(asApp, """
                INSERT INTO audit."AuditEvents" ("Id","EventType","OccurredAt","RecordedAt","Channel","SourceName","Metadata")
                VALUES (gen_random_uuid(), 'inbound.received', now(), now(), 'webhook', 'privilege-test', '{}')
                """);
            (await ScalarAsync(asApp, "SELECT count(*) FROM audit.\"AuditEvents\"")).Should().Be(1L);
            (await ScalarAsync(asApp, "SELECT count(*) FROM transactions.\"Transactions\"")).Should().Be(0L);
            (await ScalarAsync(asApp, "SELECT count(*) FROM messaging.\"InboxMessages\"")).Should().Be(0L);

            // The worker's scheduled refresh rebuilds the daily read model through a temp table.
            await ExecuteAsync(asApp, "CREATE TEMP TABLE refresh_probe (id int)");
            await ExecuteAsync(asApp, "DELETE FROM transactions.\"DailyTotals\"");

            await ShouldBeDeniedAsync(asApp, "UPDATE audit.\"AuditEvents\" SET \"Detail\" = 'tampered'", "rewrite audit history");
            await ShouldBeDeniedAsync(asApp, "DELETE FROM audit.\"AuditEvents\"", "erase audit history");
            await ShouldBeDeniedAsync(asApp, "ALTER TABLE audit.\"AuditEvents\" DISABLE TRIGGER ALL", "switch off the append-only triggers");
            await ShouldBeDeniedAsync(asApp, "DROP TABLE transactions.\"Transactions\"", "drop financial data");
            await ShouldBeDeniedAsync(asApp, "ALTER TABLE transactions.\"Transactions\" DROP CONSTRAINT \"CK_Transactions_Amount_NonZero\"", "weaken invariants");
            await ShouldBeDeniedAsync(asApp, "CREATE TABLE public.shadow (id int)", "create objects");

            (await ScalarAsync(asMigrator, "SELECT count(*) FROM pg_tables WHERE tableowner = 'tagg_migrator' AND schemaname = 'transactions'"))
                .Should().Be(3L, "the ledger, its daily read model and the read model's checkpoint");
        }

        private async Task<(string AsMigrator, string AsApp)> ProvisionAsync()
        {
            var database = $"privileges_{Guid.NewGuid():N}";
            var admin = new NpgsqlConnectionStringBuilder(_fixture.ConnectionString);
            await ExecuteAsync(admin.ConnectionString, $"CREATE DATABASE \"{database}\"");

            admin.Database = database;
            await RunInitRolesScriptAsync(admin.ConnectionString);

            var asMigrator = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Username = "tagg_migrator", Password = MigratorPassword }.ConnectionString;
            var asApp = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Username = "tagg_app", Password = AppPassword }.ConnectionString;

            await using (var messaging = _fixture.CreateMessagingContext(asMigrator)) await messaging.Database.MigrateAsync();
            await using (var transactions = _fixture.CreateContext(connectionString: asMigrator)) await transactions.Database.MigrateAsync();
            await using (var webhookSources = _fixture.CreateWebhookSourcesContext(asMigrator)) await webhookSources.Database.MigrateAsync();
            await using (var audit = _fixture.CreateAuditContext(asMigrator)) await audit.Database.MigrateAsync();

            return (asMigrator, asApp);
        }

        [GeneratedRegex(@"<<'SQL'\n(?<sql>.*?)\n\s*SQL\n", RegexOptions.Singleline)]
        private static partial Regex SqlBlock();

        /// <summary>
        /// Executes the SQL heredoc from init-roles.yaml the way psql would: psql variables are
        /// substituted and each "SELECT format(...) \gexec" runs the statements it generates.
        /// </summary>
        private static async Task RunInitRolesScriptAsync(string superuserConnectionString)
        {
            var yaml = (await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "k8s", "postgres", "init-roles.yaml"))).Replace("\r\n", "\n");
            var sql = SqlBlock().Match(yaml).Groups["sql"].Value
                .Replace(":'migrator_pw'", $"'{MigratorPassword}'")
                .Replace(":'app_pw'", $"'{AppPassword}'");

            await using var connection = new NpgsqlConnection(superuserConnectionString);
            await connection.OpenAsync();

            foreach (var raw in Regex.Split(sql, @"(?<=;)\s*\n|(?<=\\gexec)\s*\n"))
            {
                var statement = string.Join('\n', raw.Split('\n').Where(l => !l.TrimStart().StartsWith("--"))).Trim();
                if (statement.Length == 0)
                    continue;

                if (statement.EndsWith(@"\gexec", StringComparison.Ordinal))
                {
                    var generated = new List<string>();
                    await using (var query = new NpgsqlCommand(statement[..^@"\gexec".Length], connection))
                    await using (var reader = await query.ExecuteReaderAsync())
                        while (await reader.ReadAsync())
                            generated.Add(reader.GetString(0));

                    foreach (var command in generated)
                        await new NpgsqlCommand(command, connection).ExecuteNonQueryAsync();
                }
                else
                {
                    await new NpgsqlCommand(statement, connection).ExecuteNonQueryAsync();
                }
            }
        }

        private static async Task ShouldBeDeniedAsync(string connectionString, string sql, string because)
        {
            var act = () => ExecuteAsync(connectionString, sql);
            (await act.Should().ThrowAsync<PostgresException>($"the application role must not be able to {because}"))
                .Which.SqlState.Should().BeOneOf(PostgresErrorCodes.InsufficientPrivilege);
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

        private static string RepositoryRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TransactionAggregationAPI.slnx")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }
}