using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.WebhookSources.Infrastructure.Migrations
{
    public partial class AddWebhookSourceAuthorizedInstitutions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "AuthorizedInstitutions",
                schema: "webhooksources",
                table: "WebhookSources",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorizedInstitutions",
                schema: "webhooksources",
                table: "WebhookSources");
        }
    }
}