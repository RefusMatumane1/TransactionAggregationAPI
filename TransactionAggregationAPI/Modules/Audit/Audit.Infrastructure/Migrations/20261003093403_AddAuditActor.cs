using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Audit.Infrastructure.Migrations
{
    // Administrative changes are attributed to the user who made them. A nullable column is a
    // metadata-only change; the index is built CONCURRENTLY so the append-only table keeps taking writes.
    /// <inheritdoc />
    public partial class AddAuditActor : Migration
    {
        private const string ActorIndex = "IX_AuditEvents_Actor_OccurredAt";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Actor",
                schema: "audit",
                table: "AuditEvents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql($"""DROP INDEX CONCURRENTLY IF EXISTS audit."{ActorIndex}";""", suppressTransaction: true);
            migrationBuilder.Sql(
                $"""CREATE INDEX CONCURRENTLY "{ActorIndex}" ON audit."AuditEvents" ("Actor", "OccurredAt") WHERE "Actor" IS NOT NULL;""",
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""DROP INDEX CONCURRENTLY IF EXISTS audit."{ActorIndex}";""", suppressTransaction: true);

            migrationBuilder.DropColumn(
                name: "Actor",
                schema: "audit",
                table: "AuditEvents");
        }
    }
}