using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Audit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                schema: "audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExternalAccountId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalTransactionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Detail = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false),
                    TraceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_Channel_Source_OccurredAt",
                schema: "audit",
                table: "AuditEvents",
                columns: new[] { "Channel", "SourceName", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EventType_OccurredAt",
                schema: "audit",
                table: "AuditEvents",
                columns: new[] { "EventType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ExternalAccountId_OccurredAt",
                schema: "audit",
                table: "AuditEvents",
                columns: new[] { "ExternalAccountId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_InboxMessageId",
                schema: "audit",
                table: "AuditEvents",
                column: "InboxMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OccurredAt",
                schema: "audit",
                table: "AuditEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TransactionId",
                schema: "audit",
                table: "AuditEvents",
                column: "TransactionId");

            // Append-only at the database level, not just by convention in code: any UPDATE,
            // DELETE or TRUNCATE — from the app, a migration, or someone with psql — fails.
            // A retention purge has to be a deliberate, reviewable act (disable the trigger in
            // its own migration/script), never an accident. See docs/data-retention.md.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION audit.prevent_audit_event_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    RAISE EXCEPTION 'audit.AuditEvents is append-only (% rejected)', TG_OP
                        USING ERRCODE = 'insufficient_privilege';
                END;
                $$;

                CREATE TRIGGER "TR_AuditEvents_AppendOnly"
                    BEFORE UPDATE OR DELETE ON audit."AuditEvents"
                    FOR EACH ROW EXECUTE FUNCTION audit.prevent_audit_event_mutation();

                CREATE TRIGGER "TR_AuditEvents_NoTruncate"
                    BEFORE TRUNCATE ON audit."AuditEvents"
                    FOR EACH STATEMENT EXECUTE FUNCTION audit.prevent_audit_event_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS "TR_AuditEvents_NoTruncate" ON audit."AuditEvents";
                DROP TRIGGER IF EXISTS "TR_AuditEvents_AppendOnly" ON audit."AuditEvents";
                DROP FUNCTION IF EXISTS audit.prevent_audit_event_mutation();
                """);

            migrationBuilder.DropTable(
                name: "AuditEvents",
                schema: "audit");
        }
    }
}