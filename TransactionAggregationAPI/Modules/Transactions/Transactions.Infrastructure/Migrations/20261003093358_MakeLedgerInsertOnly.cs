using BuildingBlocks.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Modules.Transactions.Infrastructure.Persistence.Configurations;

#nullable disable

namespace Modules.Transactions.Infrastructure.Migrations
{
    // The ledger becomes insert-only and multi-currency. Schema only: no row is
    // updated or deleted. Rows left Pending or Expired by the earlier lifecycle stay as they are and
    // fall outside every ledger index (all partial on Status = Booked), so a later posting of the same
    // bank transaction is recorded as a new ledger entry instead of changing the old row.
    //
    // Every index is built CONCURRENTLY (no write lock on a live table) and each statement is
    // re-runnable after a partial failure.
    /// <inheritdoc />
    public partial class MakeLedgerInsertOnly : Migration
    {
        public const string LedgerKeyIndex = "IX_Transactions_Ledger_Key";
        public const string DateCoveringIndex = "IX_Transactions_Date_Id_Covering";
        public const string AmountIndex = "IX_Transactions_Amount_Id";
        public const string AccountIndex = "IX_Transactions_Account_Date_Id";
        public const string DescriptionSearchIndex = "IX_Transactions_Description_Trgm";

        private const string Booked = "\"Status\" = 4";
        private const string Table = "transactions.\"Transactions\"";
        private const string PreviousKeyIndex = "IX_Transactions_Institution_Account_ExternalId_Unique";
        private const string DateCoveringIndexNew = "IX_Transactions_Date_Id_Covering_New";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Trusted extension: creatable by the schema owner; a no-op where the cluster bootstrap
            // already created it.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            BuildConcurrently(migrationBuilder, LedgerKeyIndex,
                $"CREATE UNIQUE INDEX CONCURRENTLY \"{LedgerKeyIndex}\" ON {Table} (\"SourceName\", \"ExternalAccountId\", \"SourceExternalId\") WHERE {Booked}");
            DropConcurrently(migrationBuilder, PreviousKeyIndex);

            BuildConcurrently(migrationBuilder, DateCoveringIndexNew,
                $"CREATE INDEX CONCURRENTLY \"{DateCoveringIndexNew}\" ON {Table} (\"Date\" DESC, \"Id\" DESC) " +
                $"INCLUDE (\"Amount\", \"Currency\", \"Category\", \"SourceName\", \"ExternalAccountId\") WHERE {Booked}");
            DropConcurrently(migrationBuilder, DateCoveringIndex);
            migrationBuilder.Sql($"ALTER INDEX transactions.\"{DateCoveringIndexNew}\" RENAME TO \"{DateCoveringIndex}\";");

            BuildConcurrently(migrationBuilder, AmountIndex,
                $"CREATE INDEX CONCURRENTLY \"{AmountIndex}\" ON {Table} (\"Amount\", \"Id\") WHERE {Booked}");
            BuildConcurrently(migrationBuilder, AccountIndex,
                $"CREATE INDEX CONCURRENTLY \"{AccountIndex}\" ON {Table} (\"SourceName\", \"ExternalAccountId\", \"Date\" DESC, \"Id\" DESC) WHERE {Booked}");
            BuildConcurrently(migrationBuilder, DescriptionSearchIndex,
                $"CREATE INDEX CONCURRENTLY \"{DescriptionSearchIndex}\" ON {Table} USING gin (\"Description\" gin_trgm_ops) WHERE {Booked}");

            // Served only the removed pending-expiry job.
            DropConcurrently(migrationBuilder, "IX_Transactions_Pending_CreatedAt");

            migrationBuilder.Sql($"ALTER TABLE {Table} DROP CONSTRAINT IF EXISTS \"CK_Transactions_Currency_Supported\";");
            migrationBuilder.AddCheckConstraintWithoutBlockingWrites(
                "transactions", "Transactions",
                TransactionConfiguration.CurrencyConstraintName, TransactionConfiguration.CurrencyConstraintSql);

            // The database refuses to change or remove a ledger entry, whoever asks.
            migrationBuilder.Sql($"""
                CREATE OR REPLACE FUNCTION transactions.prevent_ledger_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'transactions.Transactions is an insert-only ledger (% rejected)', TG_OP
                        USING ERRCODE = 'insufficient_privilege';
                END;
                $$;

                DROP TRIGGER IF EXISTS "TR_Transactions_InsertOnly" ON {Table};
                CREATE TRIGGER "TR_Transactions_InsertOnly"
                    BEFORE UPDATE OR DELETE ON {Table}
                    FOR EACH ROW EXECUTE FUNCTION transactions.prevent_ledger_mutation();

                DROP TRIGGER IF EXISTS "TR_Transactions_NoTruncate" ON {Table};
                CREATE TRIGGER "TR_Transactions_NoTruncate"
                    BEFORE TRUNCATE ON {Table}
                    FOR EACH STATEMENT EXECUTE FUNCTION transactions.prevent_ledger_mutation();

                DO $$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'tagg_app') THEN
                        REVOKE UPDATE, DELETE, TRUNCATE ON {Table} FROM tagg_app;
                        GRANT SELECT, INSERT ON {Table} TO tagg_app;
                    END IF;
                END $$;
                """);

            // Npgsql emits no SQL for the xmin system column; this only removes the concurrency token
            // from the model, since a row that is never updated needs none.
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "transactions",
                table: "Transactions");
        }

        // Restores the previous shape. The unique key over all rows can only be rebuilt if no bank
        // transaction has both a legacy pending row and a ledger entry; production rolls forward.
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DO $$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'tagg_app') THEN
                        GRANT UPDATE, DELETE ON {Table} TO tagg_app;
                    END IF;
                END $$;

                DROP TRIGGER IF EXISTS "TR_Transactions_NoTruncate" ON {Table};
                DROP TRIGGER IF EXISTS "TR_Transactions_InsertOnly" ON {Table};
                DROP FUNCTION IF EXISTS transactions.prevent_ledger_mutation();
                """);

            migrationBuilder.Sql($"ALTER TABLE {Table} DROP CONSTRAINT IF EXISTS \"{TransactionConfiguration.CurrencyConstraintName}\";");
            migrationBuilder.AddCheckConstraintWithoutBlockingWrites(
                "transactions", "Transactions", "CK_Transactions_Currency_Supported", "\"Currency\" = 'ZAR'");

            BuildConcurrently(migrationBuilder, "IX_Transactions_Pending_CreatedAt",
                $"CREATE INDEX CONCURRENTLY \"IX_Transactions_Pending_CreatedAt\" ON {Table} (\"CreatedAt\") WHERE \"Status\" = 0");

            DropConcurrently(migrationBuilder, DescriptionSearchIndex);
            DropConcurrently(migrationBuilder, AccountIndex);
            DropConcurrently(migrationBuilder, AmountIndex);

            BuildConcurrently(migrationBuilder, DateCoveringIndexNew,
                $"CREATE INDEX CONCURRENTLY \"{DateCoveringIndexNew}\" ON {Table} (\"Date\" DESC, \"Id\" DESC) " +
                "INCLUDE (\"Amount\", \"Status\", \"Category\", \"SourceName\", \"ExternalAccountId\")");
            DropConcurrently(migrationBuilder, DateCoveringIndex);
            migrationBuilder.Sql($"ALTER INDEX transactions.\"{DateCoveringIndexNew}\" RENAME TO \"{DateCoveringIndex}\";");

            BuildConcurrently(migrationBuilder, PreviousKeyIndex,
                $"CREATE UNIQUE INDEX CONCURRENTLY \"{PreviousKeyIndex}\" ON {Table} (\"SourceName\", \"ExternalAccountId\", \"SourceExternalId\")");
            DropConcurrently(migrationBuilder, LedgerKeyIndex);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "transactions",
                table: "Transactions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        // A failed CONCURRENTLY build leaves an INVALID index that IF NOT EXISTS would silently keep,
        // so any leftover is dropped and the index built afresh.
        private static void BuildConcurrently(MigrationBuilder migrationBuilder, string name, string createSql)
        {
            DropConcurrently(migrationBuilder, name);
            migrationBuilder.Sql(createSql + ";", suppressTransaction: true);
        }

        private static void DropConcurrently(MigrationBuilder migrationBuilder, string name) =>
            migrationBuilder.Sql($"DROP INDEX CONCURRENTLY IF EXISTS transactions.\"{name}\";", suppressTransaction: true);
    }
}