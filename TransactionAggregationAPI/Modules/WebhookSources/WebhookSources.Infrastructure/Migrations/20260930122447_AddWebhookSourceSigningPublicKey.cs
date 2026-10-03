using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.WebhookSources.Infrastructure.Migrations
{
    public partial class AddWebhookSourceSigningPublicKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SigningPublicKey",
                schema: "webhooksources",
                table: "WebhookSources",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SigningPublicKey",
                schema: "webhooksources",
                table: "WebhookSources");
        }
    }
}