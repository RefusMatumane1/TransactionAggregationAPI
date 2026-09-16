using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransactionAggregation.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebhookSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookSources", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSources_KeyHash",
                table: "WebhookSources",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSources_Name",
                table: "WebhookSources",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebhookSources");
        }
    }
}
