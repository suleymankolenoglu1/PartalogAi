using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropEmbedTargetsFromModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "EmbedTargets";""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmbedTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogId = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogPageId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CommerceMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EmbedKey = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    ExistingCartMethod = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ExistingCartUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    HostActionMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProductUrlTemplate = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    SearchUrlTemplate = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmbedTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmbedTargets_CatalogPages_CatalogPageId",
                        column: x => x.CatalogPageId,
                        principalTable: "CatalogPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EmbedTargets_Catalogs_CatalogId",
                        column: x => x.CatalogId,
                        principalTable: "Catalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmbedTargets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmbedTargets_CatalogId",
                table: "EmbedTargets",
                column: "CatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_EmbedTargets_CatalogPageId",
                table: "EmbedTargets",
                column: "CatalogPageId");

            migrationBuilder.CreateIndex(
                name: "IX_EmbedTargets_EmbedKey",
                table: "EmbedTargets",
                column: "EmbedKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmbedTargets_UserId_Type_IsActive",
                table: "EmbedTargets",
                columns: new[] { "UserId", "Type", "IsActive" });
        }
    }
}
