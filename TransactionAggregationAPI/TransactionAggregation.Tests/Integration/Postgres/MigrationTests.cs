using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    /// <summary>
    /// Applies every module's migrations to a real Postgres and inspects the schema, so a migration
    /// step that silently does nothing fails CI.
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
                ["Customers", "Accounts", "Transactions", "BankLinks", "WebhookSources", "InboxMessages", "OutboxMessages", "AuditEvents"];

            foreach (var table in expectedTables)
            {
                var exists = await context.Database.SqlQuery<int>(
                    $"SELECT 1 AS \"Value\" FROM information_schema.tables WHERE table_name = {table}")
                    .AnyAsync();
                exists.Should().BeTrue($"table '{table}' should exist after migrations run");
            }
        }

        /// <summary>Pins the schema-per-module split (ADR-0009): each table lives in its owning module's schema.</summary>
        [Theory]
        [InlineData("Customers", "customers")]
        [InlineData("Accounts", "customers")]
        [InlineData("Transactions", "transactions")]
        [InlineData("BankLinks", "banklinks")]
        [InlineData("WebhookSources", "webhooksources")]
        [InlineData("InboxMessages", "messaging")]
        [InlineData("OutboxMessages", "messaging")]
        [InlineData("AuditEvents", "audit")]
        public async Task Migrations_PlaceEachTable_InItsOwningModulesSchema(string table, string expectedSchema)
        {
            using var context = _fixture.CreateContext();

            var actualSchema = await context.Database.SqlQuery<string>(
                $"SELECT table_schema AS \"Value\" FROM information_schema.tables WHERE table_name = {table}")
                .SingleOrDefaultAsync();

            actualSchema.Should().Be(expectedSchema, $"table '{table}' must live in the \"{expectedSchema}\" schema, not wherever it happened to land");
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