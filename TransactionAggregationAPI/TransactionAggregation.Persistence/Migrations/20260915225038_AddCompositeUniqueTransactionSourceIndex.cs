using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransactionAggregation.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompositeUniqueTransactionSourceIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF Core's fluent HasIndex() can't express an index spanning an owner property
            // (CustomerId) and an owned navigation's property (Source.ExternalId) mapped
            // into the same table, so this is raw SQL instead of a model-driven index —
            // see the comment in TransactionConfiguration.Configure().
            //
            // NOTE: if any environment already has duplicate (CustomerId, SourceExternalId)
            // rows — e.g. from the exact double-insert race this index is meant to prevent —
            // this migration will fail until those duplicates are de-duplicated first.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_Transactions_Customer_SourceExternalId_Unique"
                ON "Transactions" ("CustomerId", "SourceExternalId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_Transactions_Customer_SourceExternalId_Unique";
                """);
        }
    }
}