using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Transactions.Infrastructure.Migrations
{
    public partial class BackfillSettledStatusForIngestedTransactions : Migration
    {
        private const int Pending = 0;
        private const int Settled = 4;

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                UPDATE transactions."Transactions"
                SET "Status" = {Settled},
                    "UpdatedAt" = now()
                WHERE "Status" = {Pending}
                  AND "SourceExternalId" NOT LIKE 'seed-%';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}