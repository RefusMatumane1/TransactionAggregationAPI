using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Transactions.Infrastructure.Migrations
{
    /// <summary>
    /// Data-only migration. Before the bank-driven settlement change, ingestion stored every
    /// transaction as Pending (0) and nothing ever moved it on. Payloads back then carried no
    /// status, and "no status" now means posted — so those rows should have been Settled (4)
    /// all along, and until they are they're missing from every total and balance.
    ///
    /// Only rows ingested from a bank are touched. The seeder is the only other creation path,
    /// and its rows (external ids prefixed "seed-") are deliberately given a mix of statuses,
    /// Pending included — those stay as they are.
    ///
    /// Safe to run before the new code serves traffic: migrations run ahead of the rollout
    /// (the db-migrate Job in k8s, or startup in Development), so at this point every
    /// non-seed Pending row was written by the old code. Rows an old pod ingests during a
    /// rolling deploy *after* this ran stay Pending — re-run the UPDATE below by hand if that
    /// window matters for you.
    /// </summary>
    public partial class BackfillSettledStatusForIngestedTransactions : Migration
    {
        private const int Pending = 0;
        private const int Settled = 4;

        /// <inheritdoc />
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

        /// <summary>
        /// Deliberately a no-op. After Up, a backfilled row is indistinguishable from one the
        /// bank genuinely posted, so reverting "every non-seed Settled row" would also un-settle
        /// real postings. Rolling back the code is safe without this: the old code simply
        /// never reads the status when summing.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}