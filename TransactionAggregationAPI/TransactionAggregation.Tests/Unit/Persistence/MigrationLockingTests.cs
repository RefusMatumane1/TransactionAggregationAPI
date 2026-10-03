using BuildingBlocks.Messaging.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Modules.Audit.Infrastructure.Persistence;
using Modules.Transactions.Infrastructure.Persistence;
using Modules.WebhookSources.Infrastructure.Persistence;
using System.Reflection;
using Xunit;

namespace TransactionAggregation.Tests.Unit.Persistence
{
    public class MigrationLockingTests
    {
        private static readonly Assembly[] MigrationAssemblies =
        [
            typeof(TransactionsDbContext).Assembly,
            typeof(MessagingDbContext).Assembly,
            typeof(AuditDbContext).Assembly,
            typeof(WebhookSourcesDbContext).Assembly
        ];

        public static TheoryData<string> MigrationsThatValidateConstraints()
        {
            var data = new TheoryData<string>();
            foreach (var (name, _) in ValidateStatements().DistinctBy(s => s.Migration))
                data.Add(name);
            return data;
        }

        private static IEnumerable<(string Migration, SqlOperation Operation)> ValidateStatements() =>
            MigrationAssemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsSubclassOf(typeof(Migration)) && !t.IsAbstract)
                .SelectMany(t => ((Migration)Activator.CreateInstance(t)!).UpOperations
                    .OfType<SqlOperation>()
                    .Where(o => o.Sql.Contains("VALIDATE CONSTRAINT", StringComparison.OrdinalIgnoreCase))
                    .Select(o => (t.Name, o)));

        [Fact]
        public void TheGuardFindsTheMigrationsItProtects() =>
            MigrationsThatValidateConstraints().Should().HaveCountGreaterThanOrEqualTo(3,
                "the transactions invariants, the transactions ZAR check and the accounts ZAR check all validate constraints");

        [Theory]
        [MemberData(nameof(MigrationsThatValidateConstraints))]
        public void ValidateConstraint_RunsOutsideTheTransactionThatAddedTheConstraint(string migration)
        {
            foreach (var (_, operation) in ValidateStatements().Where(s => s.Migration == migration))
            {
                operation.SuppressTransaction.Should().BeTrue(
                    $"{migration}: VALIDATE inside the migration transaction would keep the ADD's ACCESS EXCLUSIVE lock for the whole scan");
                operation.Sql.Should().NotContainEquivalentOf("ADD CONSTRAINT",
                    $"{migration}: the ADD must commit before VALIDATE starts");
            }
        }
    }
}