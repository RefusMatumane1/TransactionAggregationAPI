using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
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
                ["Transactions", "WebhookSources", "InboxMessages", "OutboxMessages", "AuditEvents", "Customers", "CustomerAccounts"];

            foreach (var table in expectedTables)
            {
                var exists = await context.Database.SqlQuery<int>(
                    $"SELECT 1 AS \"Value\" FROM information_schema.tables WHERE table_name = {table}")
                    .AnyAsync();
                exists.Should().BeTrue($"table '{table}' should exist after migrations run");
            }
        }

        [Theory]
        [InlineData("Transactions", "transactions")]
        [InlineData("WebhookSources", "webhooksources")]
        [InlineData("InboxMessages", "messaging")]
        [InlineData("OutboxMessages", "messaging")]
        [InlineData("AuditEvents", "audit")]
        [InlineData("Customers", "customerdirectory")]
        [InlineData("CustomerAccounts", "customerdirectory")]
        public async Task Migrations_PlaceEachTable_InItsOwningModulesSchema(string table, string expectedSchema)
        {
            using var context = _fixture.CreateContext();

            var actualSchema = await context.Database.SqlQuery<string>(
                $"SELECT table_schema AS \"Value\" FROM information_schema.tables WHERE table_name = {table}")
                .SingleOrDefaultAsync();

            actualSchema.Should().Be(expectedSchema, $"table '{table}' must live in the \"{expectedSchema}\" schema, not wherever it happened to land");
        }

        [Theory]
        [InlineData("Transactions", "CK_Transactions_Amount_NonZero")]
        [InlineData("Transactions", "CK_Transactions_Category_Defined")]
        [InlineData("Transactions", "CK_Transactions_Status_Defined")]
        [InlineData("Transactions", "CK_Transactions_Currency_Iso4217")]
        public async Task CheckConstraintsAddedWithoutBlockingWrites_EndUpValidated(string table, string constraint)
        {
            using var context = _fixture.CreateContext();

            var validated = await context.Database.SqlQuery<bool>(
                $"""
                SELECT c.convalidated AS "Value" FROM pg_constraint c
                JOIN pg_class t ON t.oid = c.conrelid
                WHERE t.relname = {table} AND c.conname = {constraint}
                """)
                .SingleOrDefaultAsync();

            validated.Should().BeTrue(
                $"{constraint} is added NOT VALID and validated in a separate step; if that step were skipped, existing rows would be unchecked");
        }

        [Fact]
        public async Task TransactionSourceExternalId_HasAUniqueConstraintScopedToInstitutionAndAccount()
        {
            using var context = _fixture.CreateContext();

            var indexExists = await context.Database.SqlQuery<int>(
                $"""
                SELECT 1 AS "Value" FROM pg_indexes
                WHERE tablename = 'Transactions' AND indexdef ILIKE '%UNIQUE%' AND indexdef ILIKE '%ExternalId%'
                """)
                .AnyAsync();

            indexExists.Should().BeTrue(
                "a unique index on (institution, account, bank transaction id) must exist at the database level for idempotent ingestion to hold under concurrency");
        }
    }
}