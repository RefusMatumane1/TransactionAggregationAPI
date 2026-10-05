using Microsoft.EntityFrameworkCore.Migrations;

namespace BuildingBlocks.Persistence.Migrations
{
    public static class MigrationBuilderExtensions
    {
        // ADD ... NOT VALID holds ACCESS EXCLUSIVE only until it commits; VALIDATE then scans under
        // SHARE UPDATE EXCLUSIVE, which lets reads and writes continue. Run in one transaction, the
        // ADD's lock would be held for the whole scan, so VALIDATE runs as its own suppressed command.
        public static void AddCheckConstraintWithoutBlockingWrites(
            this MigrationBuilder migrationBuilder, string schema, string table, string name, string sql)
        {
            var qualifiedTable = $"{schema}.\"{table}\"";

            migrationBuilder.Sql($"""
                ALTER TABLE {qualifiedTable} DROP CONSTRAINT IF EXISTS "{name}";
                ALTER TABLE {qualifiedTable} ADD CONSTRAINT "{name}" CHECK ({sql}) NOT VALID;
                """);

            migrationBuilder.Sql(
                $"""ALTER TABLE {qualifiedTable} VALIDATE CONSTRAINT "{name}";""",
                suppressTransaction: true);
        }
    }
}