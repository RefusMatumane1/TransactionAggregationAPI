using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Messaging.Migrations
{
    public partial class AddInboxIdempotencyKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "messaging",
                table: "InboxMessages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_SourceName_IdempotencyKey",
                schema: "messaging",
                table: "InboxMessages",
                columns: new[] { "SourceName", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InboxMessages_SourceName_IdempotencyKey",
                schema: "messaging",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "messaging",
                table: "InboxMessages");
        }
    }
}