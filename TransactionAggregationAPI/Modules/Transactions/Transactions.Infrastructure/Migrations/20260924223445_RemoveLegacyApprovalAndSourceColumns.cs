using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Transactions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyApprovalAndSourceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SourceLastSyncDate",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SourceProvider",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "SourceVersion",
                schema: "transactions",
                table: "Transactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                schema: "transactions",
                table: "Transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedBy",
                schema: "transactions",
                table: "Transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceLastSyncDate",
                schema: "transactions",
                table: "Transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceProvider",
                schema: "transactions",
                table: "Transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceVersion",
                schema: "transactions",
                table: "Transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }
    }
}