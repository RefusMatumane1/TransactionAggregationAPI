using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Transactions.Infrastructure.Migrations
{
    public partial class ScopeTransactionDedupToInstitutionAndWidenAmount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "transactions",
                table: "Transactions",
                type: "numeric(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_Transactions_Customer_Source_ExternalId_Unique"
                ON transactions."Transactions" ("CustomerId", "SourceName", "SourceExternalId");

                DROP INDEX IF EXISTS transactions."IX_Transactions_Customer_SourceExternalId_Unique";
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_Transactions_Customer_SourceExternalId_Unique"
                ON transactions."Transactions" ("CustomerId", "SourceExternalId");

                DROP INDEX IF EXISTS transactions."IX_Transactions_Customer_Source_ExternalId_Unique";
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "transactions",
                table: "Transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(19,4)",
                oldPrecision: 19,
                oldScale: 4);
        }
    }
}