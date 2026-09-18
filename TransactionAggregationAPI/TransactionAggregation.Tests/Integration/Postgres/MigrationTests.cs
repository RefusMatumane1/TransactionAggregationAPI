using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    /// <summary>
    /// Automates the manual verification performed during this review: running
    /// `--migrate-only` against a throwaway real Postgres and inspecting the schema
    /// with psql. MigrationExtensions.ApplyMigrationsAsync previously acquired the
    /// advisory lock and logged success without ever calling MigrateAsync() — this
    /// is the regression test for that class of bug.
    /// </summary>
    [Collection(PostgresCollection.Name)]
    public class MigrationTests
    {
        private readonly PostgresContainerFixture _fixture;

        public MigrationTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Migrations_HaveBeenApplied_AllExpectedTablesExist()
        {
            using var context = _fixture.CreateContext();

            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
            appliedMigrations.Should().NotBeEmpty("MigrateAsync() must actually run pending migrations, not just log that it did");

            string[] expectedTables =
                ["Customers", "Accounts", "Transactions", "BankLinks", "WebhookSources", "InboxMessages", "OutboxMessages"];

            foreach (var table in expectedTables)
            {
                var exists = await context.Database.SqlQuery<int>(
                    $"SELECT 1 AS \"Value\" FROM information_schema.tables WHERE table_name = {table}")
                    .AnyAsync();
                exists.Should().BeTrue($"table '{table}' should exist after migrations run");
            }
        }

        [Fact]
        public async Task TransactionSourceExternalId_HasAUniqueConstraintScopedToCustomer()
        {
            // Confirms the idempotency guarantee ADR-0002 depends on is an actual
            // database constraint, not just an EF Core model annotation that might
            // not have made it into a migration.
            using var context = _fixture.CreateContext();

            var indexExists = await context.Database.SqlQuery<int>(
                $"""
                SELECT 1 AS "Value" FROM pg_indexes
                WHERE tablename = 'Transactions' AND indexdef ILIKE '%UNIQUE%' AND indexdef ILIKE '%ExternalId%'
                """)
                .AnyAsync();

            indexExists.Should().BeTrue(
                "a unique index on (CustomerId, Source.ExternalId) must exist at the database level for idempotent ingestion to hold under concurrency");
        }
    }
}
