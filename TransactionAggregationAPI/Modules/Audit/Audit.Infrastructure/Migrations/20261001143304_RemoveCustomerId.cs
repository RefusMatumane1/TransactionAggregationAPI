using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Modules.Audit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCustomerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "audit",
                table: "AuditEvents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "audit",
                table: "AuditEvents",
                type: "uuid",
                nullable: true);
        }
    }
}