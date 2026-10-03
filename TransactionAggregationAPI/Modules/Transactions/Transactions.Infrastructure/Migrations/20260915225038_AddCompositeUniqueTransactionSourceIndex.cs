using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Transactions.Infrastructure.Migrations
{
    public partial class AddCompositeUniqueTransactionSourceIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_Transactions_Customer_SourceExternalId_Unique"
                ON "Transactions" ("CustomerId", "SourceExternalId");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_Transactions_Customer_SourceExternalId_Unique";
                """);
        }
    }
}