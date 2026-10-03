using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Transactions.Infrastructure.Migrations
{
    public partial class RemoveUniqueSourceId : Migration
    {
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