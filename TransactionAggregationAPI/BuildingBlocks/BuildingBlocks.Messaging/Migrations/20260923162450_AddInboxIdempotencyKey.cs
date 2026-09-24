using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Messaging.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxIdempotencyKey : Migration
    {
        /// <inheritdoc />
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

        /// <inheritdoc />
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