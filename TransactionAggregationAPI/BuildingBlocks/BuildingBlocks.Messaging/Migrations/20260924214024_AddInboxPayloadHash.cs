using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Messaging.Migrations
{
    public partial class AddInboxPayloadHash : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PayloadHash",
                schema: "messaging",
                table: "InboxMessages",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PayloadHash",
                schema: "messaging",
                table: "InboxMessages");
        }
    }
}