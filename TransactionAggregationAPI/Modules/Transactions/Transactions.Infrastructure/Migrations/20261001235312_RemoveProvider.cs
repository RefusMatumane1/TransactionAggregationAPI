using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Transactions.Infrastructure.Migrations
{
    // Each bank is its own source, so the delivering source is SourceName and Provider
    // only repeated it. Dropping a column also drops every index that INCLUDEs it, so the covering
    // index is rebuilt without Provider first (concurrently, under a temporary name) and renamed into
    // place once the old one is gone: reads never lose their index.
    /// <inheritdoc />
    public partial class RemoveProvider : Migration
    {
        private const string CoveringIndexName = "IX_Transactions_Date_Id_Covering";
        private const string NewCoveringIndexName = "IX_Transactions_Date_Id_Covering_New";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                DROP INDEX CONCURRENTLY IF EXISTS transactions."{NewCoveringIndexName}";
                """,
                suppressTransaction: true);

            migrationBuilder.Sql(
                $"""
                CREATE INDEX CONCURRENTLY "{NewCoveringIndexName}"
                ON transactions."Transactions" ("Date" DESC, "Id" DESC)
                INCLUDE ("Amount", "Status", "Category", "SourceName", "ExternalAccountId");
                """,
                suppressTransaction: true);

            migrationBuilder.DropIndex(
                name: "IX_Transactions_Provider",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.Sql($"""DROP INDEX IF EXISTS transactions."{CoveringIndexName}";""");

            migrationBuilder.DropColumn(
                name: "Provider",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.Sql($"""ALTER INDEX transactions."{NewCoveringIndexName}" RENAME TO "{CoveringIndexName}";""");
        }

        // Restores the shape only: the original values are gone, so every row gets its bank's code.
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Provider",
                schema: "transactions",
                table: "Transactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""UPDATE transactions."Transactions" SET "Provider" = "SourceName";""");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Provider",
                schema: "transactions",
                table: "Transactions",
                column: "Provider");

            migrationBuilder.Sql(
                $"""
                DROP INDEX CONCURRENTLY IF EXISTS transactions."{NewCoveringIndexName}";
                """,
                suppressTransaction: true);

            migrationBuilder.Sql(
                $"""
                CREATE INDEX CONCURRENTLY "{NewCoveringIndexName}"
                ON transactions."Transactions" ("Date" DESC, "Id" DESC)
                INCLUDE ("Amount", "Status", "Category", "SourceName", "Provider", "ExternalAccountId");
                """,
                suppressTransaction: true);

            migrationBuilder.Sql($"""DROP INDEX IF EXISTS transactions."{CoveringIndexName}";""");
            migrationBuilder.Sql($"""ALTER INDEX transactions."{NewCoveringIndexName}" RENAME TO "{CoveringIndexName}";""");
        }
    }
}