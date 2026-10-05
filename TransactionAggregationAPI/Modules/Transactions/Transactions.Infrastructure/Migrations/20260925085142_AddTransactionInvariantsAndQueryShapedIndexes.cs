using BuildingBlocks.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Transactions.Infrastructure.Migrations
{
    public partial class AddTransactionInvariantsAndQueryShapedIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_Transactions_Pending_CreatedAt"
                    ON transactions."Transactions" ("CreatedAt") WHERE "Status" = 0;
                """, suppressTransaction: true);

            foreach (var index in RedundantIndexes)
                migrationBuilder.Sql($"""DROP INDEX CONCURRENTLY IF EXISTS transactions."{index}";""", suppressTransaction: true);

            foreach (var (name, sql) in CheckConstraints)
                migrationBuilder.AddCheckConstraintWithoutBlockingWrites("transactions", "Transactions", name, sql);
        }

        private static readonly string[] RedundantIndexes =
        [
            "IX_Transactions_Category",
            "IX_Transactions_CustomerId",
            "IX_Transactions_Date",
            "IX_Transactions_SourceExternalId",
            "IX_Transactions_Status"
        ];

        private static readonly (string Name, string Sql)[] CheckConstraints =
        [
            ("CK_Transactions_Amount_NonZero", "\"Amount\" <> 0"),
            ("CK_Transactions_Category_Defined", "\"Category\" BETWEEN 0 AND 11"),
            ("CK_Transactions_Currency_Iso4217", "\"Currency\" ~ '^[A-Z]{3}$'"),
            ("CK_Transactions_Status_Defined", "\"Status\" BETWEEN 0 AND 8")
        ];

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_Pending_CreatedAt",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_Amount_NonZero",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_Category_Defined",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_Currency_Iso4217",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_Status_Defined",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Category",
                schema: "transactions",
                table: "Transactions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CustomerId",
                schema: "transactions",
                table: "Transactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Date",
                schema: "transactions",
                table: "Transactions",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SourceExternalId",
                schema: "transactions",
                table: "Transactions",
                column: "SourceExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Status",
                schema: "transactions",
                table: "Transactions",
                column: "Status");
        }
    }
}