using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransactionAggregation.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBankLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Institution = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalAccountId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EncryptedAccessToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EncryptedRefreshToken = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    TokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankLinks_CustomerId",
                table: "BankLinks",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_BankLinks_CustomerId_Institution",
                table: "BankLinks",
                columns: new[] { "CustomerId", "Institution" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankLinks");
        }
    }
}