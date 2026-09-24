using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.WebhookSources.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookSourceAuthorizedInstitutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing sources start authorized for nothing (fail closed): until an admin scopes
            // them via PUT /api/v1/admin/webhook-sources/{id}/institutions, their deliveries are
            // accepted into the inbox but dead-lettered at processing — and replaying a
            // dead-lettered delivery after scoping requeues it. Guessing a scope here would
            // silently re-open the cross-institution write this column exists to close.
            migrationBuilder.AddColumn<List<string>>(
                name: "AuthorizedInstitutions",
                schema: "webhooksources",
                table: "WebhookSources",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorizedInstitutions",
                schema: "webhooksources",
                table: "WebhookSources");
        }
    }
}