using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddErpInventorySnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ErpInventorySnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalProductId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PartCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProductName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AvailableStock = table.Column<int>(type: "integer", nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastWebhookReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpInventorySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErpInventorySnapshots_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErpInventorySnapshots_OwnerUserId_Provider_ExternalProductId",
                table: "ErpInventorySnapshots",
                columns: new[] { "OwnerUserId", "Provider", "ExternalProductId" },
                filter: "\"ExternalProductId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ErpInventorySnapshots_OwnerUserId_Provider_PartCode",
                table: "ErpInventorySnapshots",
                columns: new[] { "OwnerUserId", "Provider", "PartCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ErpInventorySnapshots_OwnerUserId_Provider_ProductId",
                table: "ErpInventorySnapshots",
                columns: new[] { "OwnerUserId", "Provider", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_ErpInventorySnapshots_ProductId",
                table: "ErpInventorySnapshots",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErpInventorySnapshots");
        }
    }
}
