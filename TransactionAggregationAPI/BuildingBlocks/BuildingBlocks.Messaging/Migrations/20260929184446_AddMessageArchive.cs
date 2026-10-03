using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BuildingBlocks.Messaging.Migrations
{
    public partial class AddMessageArchive : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboxMessagesArchive",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PayloadHash = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TraceParent = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessagesArchive", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessagesArchive",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessagesArchive", x => x.Id);
                });

            migrationBuilder.Sql(
                """CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_OutboxMessages_Processed_ProcessedAt" ON messaging."OutboxMessages" ("ProcessedAt") WHERE "Status" = 2;""",
                suppressTransaction: true);

            migrationBuilder.Sql(
                """CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_InboxMessages_Processed_ProcessedAt" ON messaging."InboxMessages" ("ProcessedAt") WHERE "Status" = 2;""",
                suppressTransaction: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessagesArchive_ReceivedAt",
                schema: "messaging",
                table: "InboxMessagesArchive",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessagesArchive_SourceName_IdempotencyKey",
                schema: "messaging",
                table: "InboxMessagesArchive",
                columns: new[] { "SourceName", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessagesArchive_OccurredAt",
                schema: "messaging",
                table: "OutboxMessagesArchive",
                column: "OccurredAt");
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboxMessagesArchive",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "OutboxMessagesArchive",
                schema: "messaging");

            migrationBuilder.Sql(
                """DROP INDEX CONCURRENTLY IF EXISTS messaging."IX_OutboxMessages_Processed_ProcessedAt";""",
                suppressTransaction: true);

            migrationBuilder.Sql(
                """DROP INDEX CONCURRENTLY IF EXISTS messaging."IX_InboxMessages_Processed_ProcessedAt";""",
                suppressTransaction: true);
        }
    }
}