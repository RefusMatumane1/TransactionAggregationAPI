using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Transactions.Infrastructure.Migrations
{
    /// <summary>
    /// Maps Transaction.Version to Postgres' built-in xmin system column as an optimistic
    /// concurrency token. Emits no DDL: Npgsql's SQL generator skips system columns, so the
    /// AddColumn below only updates EF's model snapshot (verified with `dotnet ef migrations
    /// script` — the output is just the history-table insert).
    /// </summary>
    public partial class AddTransactionConcurrencyToken : Migration
    {
        /// <inheritdoc />
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "transactions",
                table: "Transactions");
        }
    }
}