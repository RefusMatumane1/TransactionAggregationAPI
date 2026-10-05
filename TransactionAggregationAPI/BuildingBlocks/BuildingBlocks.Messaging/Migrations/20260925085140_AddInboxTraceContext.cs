using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Messaging.Migrations
{
    public partial class AddInboxTraceContext : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                schema: "messaging",
                table: "InboxMessages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceParent",
                schema: "messaging",
                table: "InboxMessages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorrelationId",
                schema: "messaging",
                table: "InboxMessages");

            migrationBuilder.DropColumn(
                name: "TraceParent",
                schema: "messaging",
                table: "InboxMessages");
        }
    }
}