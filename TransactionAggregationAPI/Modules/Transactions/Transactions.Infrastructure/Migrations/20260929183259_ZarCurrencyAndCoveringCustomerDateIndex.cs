using BuildingBlocks.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Transactions.Infrastructure.Migrations
{
    public partial class ZarCurrencyAndCoveringCustomerDateIndex : Migration
    {
        public const string CoveringIndexName = "IX_Transactions_Customer_Date_Id_Covering";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                CREATE INDEX CONCURRENTLY IF NOT EXISTS "{CoveringIndexName}"
                ON transactions."Transactions" ("CustomerId", "Date" DESC, "Id" DESC)
                INCLUDE ("Amount", "Status", "Category", "AccountId", "SourceName");
                """,
                suppressTransaction: true);

            migrationBuilder.Sql(
                """DROP INDEX CONCURRENTLY IF EXISTS transactions."IX_Transactions_Customer_Date_Category";""",
                suppressTransaction: true);

            migrationBuilder.Sql(
                """ALTER TABLE transactions."Transactions" DROP CONSTRAINT IF EXISTS "CK_Transactions_Currency_Iso4217";""");

            migrationBuilder.AddCheckConstraintWithoutBlockingWrites(
                "transactions", "Transactions", "CK_Transactions_Currency_Supported", "\"Currency\" = 'ZAR'");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_Currency_Supported",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_Currency_Iso4217",
                schema: "transactions",
                table: "Transactions",
                sql: "\"Currency\" ~ '^[A-Z]{3}$'");

            migrationBuilder.Sql(
                """
                CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Transactions_Customer_Date_Category"
                ON transactions."Transactions" ("CustomerId", "Date", "Category");
                """,
                suppressTransaction: true);

            migrationBuilder.Sql(
                $"""DROP INDEX CONCURRENTLY IF EXISTS transactions."{CoveringIndexName}";""",
                suppressTransaction: true);
        }
    }
}