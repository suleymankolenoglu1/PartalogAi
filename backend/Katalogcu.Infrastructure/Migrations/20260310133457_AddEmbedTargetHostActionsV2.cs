using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmbedTargetHostActionsV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExistingCartMethod",
                table: "EmbedTargets",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExistingCartUrl",
                table: "EmbedTargets",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HostActionMode",
                table: "EmbedTargets",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "none");

            migrationBuilder.AddColumn<string>(
                name: "ProductUrlTemplate",
                table: "EmbedTargets",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SearchUrlTemplate",
                table: "EmbedTargets",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE "EmbedTargets"
                SET "HostActionMode" = CASE
                    WHEN lower(coalesce("CommerceMode", 'catalog_only')) = 'catalog_only' THEN 'none'
                    ELSE 'existing_cart_api'
                END
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExistingCartMethod",
                table: "EmbedTargets");

            migrationBuilder.DropColumn(
                name: "ExistingCartUrl",
                table: "EmbedTargets");

            migrationBuilder.DropColumn(
                name: "HostActionMode",
                table: "EmbedTargets");

            migrationBuilder.DropColumn(
                name: "ProductUrlTemplate",
                table: "EmbedTargets");

            migrationBuilder.DropColumn(
                name: "SearchUrlTemplate",
                table: "EmbedTargets");
        }
    }
}
