using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Transactions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCustomerOwnership : Migration
    {
        public const string UniqueIndexName = "IX_Transactions_Institution_Account_ExternalId_Unique";
        public const string CoveringIndexName = "IX_Transactions_Date_Id_Covering";
        public const string LegacyValue = "legacy";

        // Transactions stop belonging to customers: each row is identified by the institution, the
        // provider's account id and the bank's transaction id instead, and the customers and banklinks
        // schemas are dropped. Ownership data is not recoverable after this runs.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalAccountId",
                schema: "transactions",
                table: "Transactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: LegacyValue);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                schema: "transactions",
                table: "Transactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: LegacyValue);

            // Rows ingested through a bank link get that link's account id back. A fresh database has
            // no banklinks schema yet (it migrates after this module), so the lookup is guarded.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('banklinks."BankLinks"') IS NOT NULL THEN
                        UPDATE transactions."Transactions" t
                        SET "ExternalAccountId" = b."ExternalAccountId"
                        FROM banklinks."BankLinks" b
                        WHERE b."AccountId" = t."AccountId" AND b."ExternalAccountId" IS NOT NULL;
                    END IF;
                END $$;
                """);

            // Joint accounts stored one copy per holder; without holders those copies are duplicates.
            migrationBuilder.Sql("""
                DELETE FROM transactions."Transactions" t
                USING (
                    SELECT "Id", row_number() OVER (
                        PARTITION BY "SourceName", "ExternalAccountId", "SourceExternalId"
                        ORDER BY "CreatedAt", "Id") AS rn
                    FROM transactions."Transactions"
                ) ranked
                WHERE t."Id" = ranked."Id" AND ranked.rn > 1;
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS transactions."IX_Transactions_Customer_Source_ExternalId_Unique";
                DROP INDEX IF EXISTS transactions."IX_Transactions_Customer_SourceExternalId_Unique";
                DROP INDEX IF EXISTS transactions."IX_Transactions_Customer_Date_Id_Covering";
                """);

            migrationBuilder.DropIndex(
                name: "IX_Transactions_AccountId",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_Customer_Status",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AccountId",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.Sql($"""
                CREATE UNIQUE INDEX "{UniqueIndexName}"
                ON transactions."Transactions" ("SourceName", "ExternalAccountId", "SourceExternalId");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Provider",
                schema: "transactions",
                table: "Transactions",
                column: "Provider");

            migrationBuilder.Sql(
                $"""
                CREATE INDEX CONCURRENTLY IF NOT EXISTS "{CoveringIndexName}"
                ON transactions."Transactions" ("Date" DESC, "Id" DESC)
                INCLUDE ("Amount", "Status", "Category", "SourceName", "Provider", "ExternalAccountId");
                """,
                suppressTransaction: true);

            migrationBuilder.Sql("""
                DROP SCHEMA IF EXISTS customers CASCADE;
                DROP SCHEMA IF EXISTS banklinks CASCADE;
                """);
        }

        // Restores the shape only: customer ownership and the dropped schemas are gone for good.
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""DROP INDEX CONCURRENTLY IF EXISTS transactions."{CoveringIndexName}";""",
                suppressTransaction: true);

            migrationBuilder.Sql($"""DROP INDEX IF EXISTS transactions."{UniqueIndexName}";""");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_Provider",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ExternalAccountId",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Provider",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                schema: "transactions",
                table: "Transactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "transactions",
                table: "Transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AccountId",
                schema: "transactions",
                table: "Transactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Customer_Status",
                schema: "transactions",
                table: "Transactions",
                columns: new[] { "CustomerId", "Status" });
        }
    }
}