using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Katalogcu.Infrastructure.Persistence;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260306193000_AddCatalogPageReviewFields")]
    public partial class AddCatalogPageReviewFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "CatalogPages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNotes",
                table: "CatalogPages",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewStatus",
                table: "CatalogPages",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "NeedsReview");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "CatalogPages");

            migrationBuilder.DropColumn(
                name: "ReviewNotes",
                table: "CatalogPages");

            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "CatalogPages");
        }
    }
}
