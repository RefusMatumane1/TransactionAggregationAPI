using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Audit.Infrastructure.Migrations
{
    public partial class RevokeAuditMutationFromApplicationRole : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'tagg_app') THEN
                        REVOKE UPDATE, DELETE, TRUNCATE ON audit."AuditEvents" FROM tagg_app;
                        GRANT SELECT, INSERT ON audit."AuditEvents" TO tagg_app;
                    END IF;
                END $$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'tagg_app') THEN
                        GRANT UPDATE, DELETE ON audit."AuditEvents" TO tagg_app;
                    END IF;
                END $$;
                """);
        }
    }
}