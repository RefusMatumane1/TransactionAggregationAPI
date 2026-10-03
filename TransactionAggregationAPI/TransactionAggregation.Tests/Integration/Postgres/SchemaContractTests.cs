using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TransactionAggregation.Tests.Integration.Postgres
{
    // Several of these indexes are created by raw SQL because EF Core cannot index owned-type
    // columns, so has-pending-model-changes cannot see them. This pins what migrations must produce.
    [Collection(PostgresCollection.Name)]
    public class SchemaContractTests(PostgresContainerFixture fixture)
    {
        public static TheoryData<string, string, Dictionary<string, string>> ExpectedIndexes() => new()
        {
            {
                "transactions", "Transactions", new()
                {
                    ["PK_Transactions"] =
                        """CREATE UNIQUE INDEX "PK_Transactions" ON transactions."Transactions" USING btree ("Id")""",
                    ["IX_Transactions_Ledger_Key"] =
                        """CREATE UNIQUE INDEX "IX_Transactions_Ledger_Key" ON transactions."Transactions" USING btree ("SourceName", "ExternalAccountId", "SourceExternalId") WHERE ("Status" = 4)""",
                    ["IX_Transactions_Date_Id_Covering"] =
                        """CREATE INDEX "IX_Transactions_Date_Id_Covering" ON transactions."Transactions" USING btree ("Date" DESC, "Id" DESC) INCLUDE ("Amount", "Currency", "Category", "SourceName", "ExternalAccountId") WHERE ("Status" = 4)""",
                    ["IX_Transactions_Amount_Id"] =
                        """CREATE INDEX "IX_Transactions_Amount_Id" ON transactions."Transactions" USING btree ("Amount", "Id") WHERE ("Status" = 4)""",
                    ["IX_Transactions_Account_Date_Id"] =
                        """CREATE INDEX "IX_Transactions_Account_Date_Id" ON transactions."Transactions" USING btree ("SourceName", "ExternalAccountId", "Date" DESC, "Id" DESC) WHERE ("Status" = 4)""",
                    ["IX_Transactions_Description_Trgm"] =
                        """CREATE INDEX "IX_Transactions_Description_Trgm" ON transactions."Transactions" USING gin ("Description" gin_trgm_ops) WHERE ("Status" = 4)""",
                    ["IX_Transactions_Booked_CreatedAt"] =
                        """CREATE INDEX "IX_Transactions_Booked_CreatedAt" ON transactions."Transactions" USING btree ("CreatedAt") WHERE ("Status" = 4)"""
                }
            },
            {
                "transactions", "DailyTotals", new()
                {
                    ["PK_DailyTotals"] =
                        """CREATE UNIQUE INDEX "PK_DailyTotals" ON transactions."DailyTotals" USING btree ("SourceName", "ExternalAccountId", "Day", "Category", "Currency")""",
                    ["IX_DailyTotals_Currency_Day"] =
                        """CREATE INDEX "IX_DailyTotals_Currency_Day" ON transactions."DailyTotals" USING btree ("Currency", "Day")"""
                }
            },
            {
                "messaging", "InboxMessages", new()
                {
                    ["PK_InboxMessages"] =
                        """CREATE UNIQUE INDEX "PK_InboxMessages" ON messaging."InboxMessages" USING btree ("Id")""",
                    ["IX_InboxMessages_SourceName_IdempotencyKey"] =
                        """CREATE UNIQUE INDEX "IX_InboxMessages_SourceName_IdempotencyKey" ON messaging."InboxMessages" USING btree ("SourceName", "IdempotencyKey") WHERE ("IdempotencyKey" IS NOT NULL)""",
                    ["IX_InboxMessages_Status_NextAttemptAt"] =
                        """CREATE INDEX "IX_InboxMessages_Status_NextAttemptAt" ON messaging."InboxMessages" USING btree ("Status", "NextAttemptAt")""",
                    ["IX_InboxMessages_Processed_ProcessedAt"] =
                        """CREATE INDEX "IX_InboxMessages_Processed_ProcessedAt" ON messaging."InboxMessages" USING btree ("ProcessedAt") WHERE ("Status" = 2)"""
                }
            },
            {
                "messaging", "OutboxMessages", new()
                {
                    ["PK_OutboxMessages"] =
                        """CREATE UNIQUE INDEX "PK_OutboxMessages" ON messaging."OutboxMessages" USING btree ("Id")""",
                    ["IX_OutboxMessages_Status_NextAttemptAt"] =
                        """CREATE INDEX "IX_OutboxMessages_Status_NextAttemptAt" ON messaging."OutboxMessages" USING btree ("Status", "NextAttemptAt")""",
                    ["IX_OutboxMessages_Processed_ProcessedAt"] =
                        """CREATE INDEX "IX_OutboxMessages_Processed_ProcessedAt" ON messaging."OutboxMessages" USING btree ("ProcessedAt") WHERE ("Status" = 2)"""
                }
            }
        };

        [Theory]
        [MemberData(nameof(ExpectedIndexes))]
        public async Task Migrations_ProduceExactlyTheIndexesTheQueriesAreWrittenAgainst(
            string schema, string table, Dictionary<string, string> expected)
        {
            using var context = fixture.CreateContext();

            var actual = (await context.Database.SqlQuery<string>(
                    $"""SELECT indexname || '|' || indexdef AS "Value" FROM pg_indexes WHERE schemaname = {schema} AND tablename = {table}""")
                .ToListAsync())
                .Select(row => row.Split('|', 2))
                .ToDictionary(parts => parts[0], parts => parts[1]);

            actual.Should().BeEquivalentTo(expected,
                $"{schema}.{table}: an index was added, dropped or changed; update this contract with it");
        }

        [Fact]
        public async Task NoIndexIsLeftInvalid_ByAnInterruptedConcurrentBuild()
        {
            using var context = fixture.CreateContext();

            var invalid = await context.Database.SqlQuery<string>(
                    $"""SELECT c.relname AS "Value" FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid WHERE NOT i.indisvalid""")
                .ToListAsync();

            invalid.Should().BeEmpty(
                "CREATE INDEX CONCURRENTLY IF NOT EXISTS skips an index left INVALID by a failed build, so it must never be relied on silently");
        }
    }
}