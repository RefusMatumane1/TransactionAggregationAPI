using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.WebhookSources.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "webhooksources");

            migrationBuilder.CreateTable(
                name: "WebhookSources",
                schema: "webhooksources",
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
                schema: "webhooksources",
                table: "WebhookSources",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSources_Name",
                schema: "webhooksources",
                table: "WebhookSources",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebhookSources",
                schema: "webhooksources");
        }
    }
}
