using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Transactions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ScopeTransactionDedupToInstitutionAndWidenAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "transactions",
                table: "Transactions",
                type: "numeric(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            // External ids are only unique per institution: the old key (CustomerId,
            // SourceExternalId) made a second bank's "txn_001" for the same customer look like
            // a duplicate and silently dropped it. The new key is strictly looser than the old
            // one, so existing data always satisfies it. Raw SQL for the same reason as
            // AddCompositeUniqueTransactionSourceIndex: EF's HasIndex can't span an owner
            // property and owned-type properties mapped into the same table.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_Transactions_Customer_Source_ExternalId_Unique"
                ON transactions."Transactions" ("CustomerId", "SourceName", "SourceExternalId");

                DROP INDEX IF EXISTS transactions."IX_Transactions_Customer_SourceExternalId_Unique";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Fails if two institutions have since stored the same external id for one
            // customer — rolling back past this point needs those rows reconciled first.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_Transactions_Customer_SourceExternalId_Unique"
                ON transactions."Transactions" ("CustomerId", "SourceExternalId");

                DROP INDEX IF EXISTS transactions."IX_Transactions_Customer_Source_ExternalId_Unique";
                """);

            // Narrowing rounds any amount carrying more than 2 decimal places.
            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "transactions",
                table: "Transactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(19,4)",
                oldPrecision: 19,
                oldScale: 4);
        }
    }
}