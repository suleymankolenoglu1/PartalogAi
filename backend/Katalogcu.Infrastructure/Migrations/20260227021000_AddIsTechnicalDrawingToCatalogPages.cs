using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260227021000_AddIsTechnicalDrawingToCatalogPages")]
    public partial class AddIsTechnicalDrawingToCatalogPages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTechnicalDrawing",
                table: "CatalogPages",
                type: "boolean",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTechnicalDrawing",
                table: "CatalogPages");
        }
    }
}
