using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Transactions.Infrastructure.Migrations
{
    public partial class AddTransactionConcurrencyToken : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "transactions",
                table: "Transactions",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "transactions",
                table: "Transactions");
        }
    }
}