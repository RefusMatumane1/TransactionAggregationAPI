using Microsoft.EntityFrameworkCore.Migrations;

namespace BuildingBlocks.Persistence.Migrations
{
    public static class MigrationBuilderExtensions
    {
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