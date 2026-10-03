using BuildingBlocks.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Transactions.Infrastructure.Migrations
{
    // Ingestion only ever produces Pending (0), Settled (4) and Expired (8). The retired values
    // (1 Approved, 2 Rejected, 3 Flagged, 5 Refunded, 6 Disputed, 7 Cancelled) were only ever written
    // by development seed data. They become Expired: balances and totals count only Settled and
    // Pending, so no customer's figures change. Literals, not the enum: this migration must keep its
    // meaning whatever the enum becomes.
    public partial class RetireUnreachableTransactionStatuses : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE transactions."Transactions"
                SET "Status" = 8, "UpdatedAt" = now()
                WHERE "Status" NOT IN (0, 4, 8);
                """);

            migrationBuilder.AddCheckConstraintWithoutBlockingWrites(
                "transactions", "Transactions", "CK_Transactions_Status_Defined", "\"Status\" IN (0, 4, 8)");
        }

        // The status mapping is not reversible; only the constraint is restored.
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Transactions_Status_Defined",
                schema: "transactions",
                table: "Transactions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Transactions_Status_Defined",
                schema: "transactions",
                table: "Transactions",
                sql: "\"Status\" BETWEEN 0 AND 8");
        }
    }
}