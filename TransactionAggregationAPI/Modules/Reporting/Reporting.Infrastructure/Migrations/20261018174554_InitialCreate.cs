using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Reporting.Infrastructure.Migrations
{
    /// <summary>
    /// The daily totals and their checkpoint used to live in the Transactions module's schema. This takes the two tables
    /// over where they stand: moving a table between schemas is a catalog change, so every row and the refresh checkpoint
    /// survive, nothing is rebuilt, and the constraint names the model expects are unchanged.
    /// The Transactions migrations always run first (see AddApplicationModules), and they create the tables on a new database.
    /// </summary>
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reporting");

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('transactions."DailyTotals"') IS NULL OR to_regclass('transactions."AggregationCheckpoints"') IS NULL THEN
                        RAISE EXCEPTION 'transactions."DailyTotals" and transactions."AggregationCheckpoints" were not found: the Transactions migrations must run before the Reporting ones.';
                    END IF;

                    ALTER TABLE transactions."DailyTotals" SET SCHEMA reporting;
                    ALTER TABLE transactions."AggregationCheckpoints" SET SCHEMA reporting;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE reporting."DailyTotals" SET SCHEMA transactions;
                ALTER TABLE reporting."AggregationCheckpoints" SET SCHEMA transactions;
                """);
        }
    }
}
