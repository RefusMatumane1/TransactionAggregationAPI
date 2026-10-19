using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Transactions.Infrastructure.Migrations
{
    /// <summary>
    /// The daily totals read model now belongs to the Reporting module, so the Transactions model stops mapping its tables.
    /// The tables themselves are NOT dropped: Reporting's InitialCreate migration moves them, with their rows, into its own
    /// schema. Dropping them here would throw the read model away; the generated DropTable was removed on purpose.
    /// </summary>
    public partial class ReleaseDailyTotalsToReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
