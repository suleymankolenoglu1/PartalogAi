using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalSiteCrawlingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalSites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PreferredCrawlMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastCrawlAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSuccessfulCrawlAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalSites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalSites_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalSiteCrawls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalSiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExecutionMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProductCount = table.Column<int>(type: "integer", nullable: false),
                    SkuCoverage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    OemCoverage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    ErrorSummary = table.Column<string>(type: "text", nullable: true),
                    RawStatsJson = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalSiteCrawls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalSiteCrawls_ExternalSites_ExternalSiteId",
                        column: x => x.ExternalSiteId,
                        principalTable: "ExternalSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ManualImportFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalSiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FileType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ImportedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ErrorSummary = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualImportFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManualImportFiles_ExternalSites_ExternalSiteId",
                        column: x => x.ExternalSiteId,
                        principalTable: "ExternalSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ManualImportFiles_Users_ImportedByUserId",
                        column: x => x.ImportedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalProducts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalSiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastSeenInCrawlId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    CanonicalUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Sku = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PartCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Brand = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CategoryPathJson = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    AvailabilityText = table.Column<string>(type: "text", nullable: true),
                    PriceText = table.Column<string>(type: "text", nullable: true),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    RawPayloadJson = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalProducts_ExternalSiteCrawls_LastSeenInCrawlId",
                        column: x => x.LastSeenInCrawlId,
                        principalTable: "ExternalSiteCrawls",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExternalProducts_ExternalSites_ExternalSiteId",
                        column: x => x.ExternalSiteId,
                        principalTable: "ExternalSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogItemExternalMatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogId = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogPageId = table.Column<Guid>(type: "uuid", nullable: true),
                    CatalogItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalSiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalProductUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ExternalProductTitle = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ConfidenceScore = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MatchedBy = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    MatchReasonsJson = table.Column<string>(type: "text", nullable: true),
                    LastLinkCheckAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLinkStatusCode = table.Column<int>(type: "integer", nullable: true),
                    IsLinkHealthy = table.Column<bool>(type: "boolean", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogItemExternalMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogItemExternalMatches_CatalogItems_CatalogItemId",
                        column: x => x.CatalogItemId,
                        principalTable: "CatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CatalogItemExternalMatches_CatalogPages_CatalogPageId",
                        column: x => x.CatalogPageId,
                        principalTable: "CatalogPages",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CatalogItemExternalMatches_Catalogs_CatalogId",
                        column: x => x.CatalogId,
                        principalTable: "Catalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CatalogItemExternalMatches_ExternalProducts_ExternalProduct~",
                        column: x => x.ExternalProductId,
                        principalTable: "ExternalProducts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CatalogItemExternalMatches_ExternalSites_ExternalSiteId",
                        column: x => x.ExternalSiteId,
                        principalTable: "ExternalSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CatalogItemExternalMatches_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ExternalProductLinkChecks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Method = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    IsReachable = table.Column<bool>(type: "boolean", nullable: false),
                    FinalUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ErrorSummary = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalProductLinkChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalProductLinkChecks_ExternalProducts_ExternalProductId",
                        column: x => x.ExternalProductId,
                        principalTable: "ExternalProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExternalProductOemNumbers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    NormalizedOemNumber = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OriginalOemNumber = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalProductOemNumbers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalProductOemNumbers_ExternalProducts_ExternalProductId",
                        column: x => x.ExternalProductId,
                        principalTable: "ExternalProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItemExternalMatches_CatalogId",
                table: "CatalogItemExternalMatches",
                column: "CatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItemExternalMatches_CatalogItemId_ConfidenceScore",
                table: "CatalogItemExternalMatches",
                columns: new[] { "CatalogItemId", "ConfidenceScore" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItemExternalMatches_CatalogItemId_Status_IsActive",
                table: "CatalogItemExternalMatches",
                columns: new[] { "CatalogItemId", "Status", "IsActive" },
                unique: true,
                filter: "\"Status\" = 'approved' AND \"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItemExternalMatches_CatalogPageId",
                table: "CatalogItemExternalMatches",
                column: "CatalogPageId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItemExternalMatches_ExternalProductId",
                table: "CatalogItemExternalMatches",
                column: "ExternalProductId");

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItemExternalMatches_ExternalSiteId_Status",
                table: "CatalogItemExternalMatches",
                columns: new[] { "ExternalSiteId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogItemExternalMatches_ReviewedByUserId",
                table: "CatalogItemExternalMatches",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalProductLinkChecks_ExternalProductId_CheckedAtUtc",
                table: "ExternalProductLinkChecks",
                columns: new[] { "ExternalProductId", "CheckedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalProductOemNumbers_ExternalProductId_NormalizedOemNu~",
                table: "ExternalProductOemNumbers",
                columns: new[] { "ExternalProductId", "NormalizedOemNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalProductOemNumbers_NormalizedOemNumber",
                table: "ExternalProductOemNumbers",
                column: "NormalizedOemNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalProducts_ExternalSiteId_CanonicalUrl",
                table: "ExternalProducts",
                columns: new[] { "ExternalSiteId", "CanonicalUrl" },
                filter: "\"CanonicalUrl\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalProducts_ExternalSiteId_PartCode",
                table: "ExternalProducts",
                columns: new[] { "ExternalSiteId", "PartCode" },
                filter: "\"PartCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalProducts_ExternalSiteId_Sku",
                table: "ExternalProducts",
                columns: new[] { "ExternalSiteId", "Sku" },
                filter: "\"Sku\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalProducts_ExternalSiteId_SourceUrl",
                table: "ExternalProducts",
                columns: new[] { "ExternalSiteId", "SourceUrl" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalProducts_LastSeenInCrawlId",
                table: "ExternalProducts",
                column: "LastSeenInCrawlId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalSiteCrawls_ExternalSiteId_CreatedDate",
                table: "ExternalSiteCrawls",
                columns: new[] { "ExternalSiteId", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalSites_UserId_BaseUrl",
                table: "ExternalSites",
                columns: new[] { "UserId", "BaseUrl" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ManualImportFiles_ExternalSiteId_ImportedAtUtc",
                table: "ManualImportFiles",
                columns: new[] { "ExternalSiteId", "ImportedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ManualImportFiles_ImportedByUserId",
                table: "ManualImportFiles",
                column: "ImportedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CatalogItemExternalMatches");

            migrationBuilder.DropTable(
                name: "ExternalProductLinkChecks");

            migrationBuilder.DropTable(
                name: "ExternalProductOemNumbers");

            migrationBuilder.DropTable(
                name: "ManualImportFiles");

            migrationBuilder.DropTable(
                name: "ExternalProducts");

            migrationBuilder.DropTable(
                name: "ExternalSiteCrawls");

            migrationBuilder.DropTable(
                name: "ExternalSites");
        }
    }
}
