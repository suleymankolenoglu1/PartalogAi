using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompatibilityGraph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MachineModels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Brand = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Variant = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    MachineGroup = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AliasesJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineModels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartCompatibilityRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    MachineModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompatibilityLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartCompatibilityRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartCompatibilityRules_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PartCompatibilityRules_MachineModels_MachineModelId",
                        column: x => x.MachineModelId,
                        principalTable: "MachineModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MachineModels_Brand_Model_Variant",
                table: "MachineModels",
                columns: new[] { "Brand", "Model", "Variant" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartCompatibilityRules_CatalogItemId_MachineModelId",
                table: "PartCompatibilityRules",
                columns: new[] { "CatalogItemId", "MachineModelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartCompatibilityRules_MachineModelId",
                table: "PartCompatibilityRules",
                column: "MachineModelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartCompatibilityRules");

            migrationBuilder.DropTable(
                name: "MachineModels");
        }
    }
}
