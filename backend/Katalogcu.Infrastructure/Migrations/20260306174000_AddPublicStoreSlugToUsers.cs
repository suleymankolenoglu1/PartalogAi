using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260306174000_AddPublicStoreSlugToUsers")]
    public partial class AddPublicStoreSlugToUsers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicStoreSlug",
                table: "Users",
                type: "character varying(96)",
                maxLength: 96,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PublicStoreSlug",
                table: "Users",
                column: "PublicStoreSlug",
                unique: true,
                filter: "\"PublicStoreSlug\" IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_PublicStoreSlug",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PublicStoreSlug",
                table: "Users");
        }
    }
}
