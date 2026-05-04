using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransactionAggregation.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueSourceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_SourceExternalId",
                table: "Transactions");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SourceExternalId",
                table: "Transactions",
                column: "SourceExternalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_SourceExternalId",
                table: "Transactions");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SourceExternalId",
                table: "Transactions",
                column: "SourceExternalId",
                unique: true);
        }
    }
}
