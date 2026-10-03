using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.WebhookSources.Infrastructure.Migrations
{
    // A source is now one bank: its Name is the bank code, and it gets a display name and colour.
    // The authorized-institutions list is gone, because a source delivers only for its own bank.
    /// <inheritdoc />
    public partial class SourcesAreBanks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorizedInstitutions",
                schema: "webhooksources",
                table: "WebhookSources");

            migrationBuilder.AddColumn<string>(
                name: "Color",
                schema: "webhooksources",
                table: "WebhookSources",
                type: "character varying(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#6C757D");

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                schema: "webhooksources",
                table: "WebhookSources",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // Existing sources are shown under their code until an admin names them.
            migrationBuilder.Sql("""UPDATE webhooksources."WebhookSources" SET "DisplayName" = left("Name", 100);""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Color",
                schema: "webhooksources",
                table: "WebhookSources");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                schema: "webhooksources",
                table: "WebhookSources");

            migrationBuilder.AddColumn<List<string>>(
                name: "AuthorizedInstitutions",
                schema: "webhooksources",
                table: "WebhookSources",
                type: "text[]",
                nullable: false,
                defaultValue: new List<string>());
        }
    }
}