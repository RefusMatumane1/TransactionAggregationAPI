using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Messaging.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxTraceContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                schema: "messaging",
                table: "OutboxMessagesArchive",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceParent",
                schema: "messaging",
                table: "OutboxMessagesArchive",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                schema: "messaging",
                table: "OutboxMessages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TraceParent",
                schema: "messaging",
                table: "OutboxMessages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorrelationId",
                schema: "messaging",
                table: "OutboxMessagesArchive");

            migrationBuilder.DropColumn(
                name: "TraceParent",
                schema: "messaging",
                table: "OutboxMessagesArchive");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                schema: "messaging",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "TraceParent",
                schema: "messaging",
                table: "OutboxMessages");
        }
    }
}