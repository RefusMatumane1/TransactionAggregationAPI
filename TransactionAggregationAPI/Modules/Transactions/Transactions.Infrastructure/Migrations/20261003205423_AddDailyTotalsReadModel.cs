using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Transactions.Infrastructure.Migrations
{
    // The daily read model the aggregate queries read, rebuilt by the worker's scheduled refresh.
    // Schema only, and both indexes are built CONCURRENTLY: the ledger index lets each refresh find
    // the entries recorded since the last one without scanning the ledger.
    /// <inheritdoc />
    public partial class AddDailyTotalsReadModel : Migration
    {
        public const string RecordedAtIndex = "IX_Transactions_Booked_CreatedAt";
        public const string DayIndex = "IX_DailyTotals_Currency_Day";

        private const string Booked = "\"Status\" = 4";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AggregationCheckpoints",
                schema: "transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Watermark = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AsOf = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AggregationCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyTotals",
                schema: "transactions",
                columns: table => new
                {
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExternalAccountId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Income = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    Expenses = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    IncomeCount = table.Column<int>(type: "integer", nullable: false),
                    ExpenseCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyTotals", x => new { x.SourceName, x.ExternalAccountId, x.Day, x.Category, x.Currency });
                });

            BuildConcurrently(migrationBuilder, RecordedAtIndex,
                $"CREATE INDEX CONCURRENTLY \"{RecordedAtIndex}\" ON transactions.\"Transactions\" (\"CreatedAt\") WHERE {Booked}");
            BuildConcurrently(migrationBuilder, DayIndex,
                $"CREATE INDEX CONCURRENTLY \"{DayIndex}\" ON transactions.\"DailyTotals\" (\"Currency\", \"Day\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropConcurrently(migrationBuilder, DayIndex);
            DropConcurrently(migrationBuilder, RecordedAtIndex);

            migrationBuilder.DropTable(
                name: "AggregationCheckpoints",
                schema: "transactions");

            migrationBuilder.DropTable(
                name: "DailyTotals",
                schema: "transactions");
        }

        private static void BuildConcurrently(MigrationBuilder migrationBuilder, string name, string createSql)
        {
            DropConcurrently(migrationBuilder, name);
            migrationBuilder.Sql(createSql + ";", suppressTransaction: true);
        }

        private static void DropConcurrently(MigrationBuilder migrationBuilder, string name) =>
            migrationBuilder.Sql($"DROP INDEX CONCURRENTLY IF EXISTS transactions.\"{name}\";", suppressTransaction: true);
    }
}