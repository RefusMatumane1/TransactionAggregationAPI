using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace TransactionAggregation.Tests.Architecture
{
    // Migrations from the insert-only ledger onwards change the schema only: no row is updated or
    // deleted, no column or table dropped (expand/contract: a later release may drop what an
    // earlier one stopped using, after an ADR), and every index is built CONCURRENTLY.
    public partial class MigrationPolicyTests
    {
        private const string PolicyStart = "20261003000000";

        private static readonly Assembly[] MigrationAssemblies =
        [
            typeof(Modules.Transactions.Infrastructure.Persistence.TransactionsDbContext).Assembly,
            typeof(BuildingBlocks.Messaging.Persistence.MessagingDbContext).Assembly,
            typeof(Modules.WebhookSources.Infrastructure.Persistence.WebhookSourcesDbContext).Assembly,
            typeof(Modules.Audit.Infrastructure.Persistence.AuditDbContext).Assembly
        ];

        [GeneratedRegex(@"\b(UPDATE|DELETE\s+FROM|TRUNCATE)\b\s+(transactions|messaging|audit|webhooksources)\.", RegexOptions.IgnoreCase)]
        private static partial Regex DataChange();

        [GeneratedRegex(@"\bCREATE\s+(UNIQUE\s+)?INDEX\b(?!\s+CONCURRENTLY)", RegexOptions.IgnoreCase)]
        private static partial Regex BlockingIndexBuild();

        public static TheoryData<string> PolicyMigrations()
        {
            var data = new TheoryData<string>();
            foreach (var type in MigrationAssemblies.SelectMany(a => a.GetTypes()).Where(IsPolicyMigration))
                data.Add(type.AssemblyQualifiedName!);
            return data;
        }

        private static bool IsPolicyMigration(Type type) =>
            typeof(Migration).IsAssignableFrom(type)
            && type.GetCustomAttribute<MigrationAttribute>() is { } attribute
            && string.CompareOrdinal(attribute.Id, PolicyStart) >= 0;

        [Fact]
        public void ThePolicyCoversAtLeastTheInsertOnlyLedgerMigration() =>
            PolicyMigrations().Should().NotBeEmpty();

        [Theory]
        [MemberData(nameof(PolicyMigrations))]
        public void Migration_NeverChangesOrDestroysData_AndNeverBlocksWrites(string migrationType)
        {
            var migration = (Migration)Activator.CreateInstance(Type.GetType(migrationType)!)!;
            var up = migration.UpOperations;

            up.OfType<DropTableOperation>().Should().BeEmpty("a table is retired by an ADR-backed contract step, not dropped in passing");
            up.OfType<DropColumnOperation>().Where(c => c.Name != "xmin")
                .Should().BeEmpty("a column is dropped only in a contract release after nothing reads it");
            up.OfType<DeleteDataOperation>().Should().BeEmpty();
            up.OfType<UpdateDataOperation>().Should().BeEmpty();
            up.OfType<CreateIndexOperation>().Should().BeEmpty("indexes are built CONCURRENTLY through migrationBuilder.Sql");

            foreach (var sql in up.OfType<SqlOperation>().Select(o => o.Sql))
            {
                DataChange().IsMatch(sql).Should().BeFalse($"no row is updated or deleted by a migration: {sql}");
                BlockingIndexBuild().IsMatch(sql).Should().BeFalse($"index builds must not block writes: {sql}");
            }
        }
    }
}