using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Customers.Infrastructure.Migrations
{
    // Customers and the bank accounts linked to them. The ledger is untouched: a customer's
    // transactions are found through these links at read time. Both secondary indexes are built
    // CONCURRENTLY, and each index statement can be re-run after a partial failure.
    /// <inheritdoc />
    public partial class AddCustomers : Migration
    {
        public const string ReferenceIndex = "IX_Customers_Reference";
        public const string AccountLookupIndex = "IX_CustomerAccounts_Institution_ExternalAccountId";

        private const string Schema = "customerdirectory";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "customerdirectory");

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "customerdirectory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                    table.CheckConstraint("CK_Customers_Name_NotBlank", "btrim(\"Name\") <> ''");
                    table.CheckConstraint("CK_Customers_Reference_Format", "\"Reference\" ~ '^[A-Za-z0-9][A-Za-z0-9_-]*$'");
                });

            migrationBuilder.CreateTable(
                name: "CustomerAccounts",
                schema: "customerdirectory",
                columns: table => new
                {
                    Institution = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExternalAccountId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerAccounts", x => new { x.CustomerId, x.Institution, x.ExternalAccountId });
                    table.CheckConstraint("CK_CustomerAccounts_ExternalAccountId_NotBlank", "btrim(\"ExternalAccountId\") <> ''");
                    table.CheckConstraint("CK_CustomerAccounts_Institution_Format", "\"Institution\" ~ '^[A-Za-z0-9][A-Za-z0-9_-]*$'");
                    table.ForeignKey(
                        name: "FK_CustomerAccounts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "customerdirectory",
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // One customer per reference.
            BuildConcurrently(migrationBuilder, ReferenceIndex,
                $"CREATE UNIQUE INDEX CONCURRENTLY \"{ReferenceIndex}\" ON {Schema}.\"Customers\" (\"Reference\")");
            // From a bank account to the customers it is linked to (staff visibility, unlinking).
            BuildConcurrently(migrationBuilder, AccountLookupIndex,
                $"CREATE INDEX CONCURRENTLY \"{AccountLookupIndex}\" ON {Schema}.\"CustomerAccounts\" (\"Institution\", \"ExternalAccountId\")");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropConcurrently(migrationBuilder, AccountLookupIndex);
            DropConcurrently(migrationBuilder, ReferenceIndex);

            migrationBuilder.DropTable(
                name: "CustomerAccounts",
                schema: "customerdirectory");

            migrationBuilder.DropTable(
                name: "Customers",
                schema: "customerdirectory");
        }

        private static void BuildConcurrently(MigrationBuilder migrationBuilder, string name, string createSql)
        {
            DropConcurrently(migrationBuilder, name);
            migrationBuilder.Sql(createSql + ";", suppressTransaction: true);
        }

        private static void DropConcurrently(MigrationBuilder migrationBuilder, string name) =>
            migrationBuilder.Sql($"DROP INDEX CONCURRENTLY IF EXISTS {Schema}.\"{name}\";", suppressTransaction: true);
    }
}