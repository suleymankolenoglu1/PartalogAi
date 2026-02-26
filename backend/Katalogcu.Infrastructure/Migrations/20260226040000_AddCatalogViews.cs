using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260226040000_AddCatalogViews")]
    public partial class AddCatalogViews : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "CatalogViews" (
                    "Id" uuid NOT NULL,
                    "CatalogId" uuid NOT NULL,
                    "OwnerUserId" uuid NOT NULL,
                    "FingerprintHash" text NOT NULL,
                    "BucketStartUtc" timestamp with time zone NOT NULL,
                    "ViewedAtUtc" timestamp with time zone NOT NULL,
                    "Source" text NOT NULL,
                    "CreatedDate" timestamp with time zone NOT NULL,
                    "UpdatedDate" timestamp with time zone NULL,
                    CONSTRAINT "PK_CatalogViews" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_CatalogViews_Catalogs_CatalogId"
                        FOREIGN KEY ("CatalogId") REFERENCES "Catalogs" ("Id") ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_CatalogViews_CatalogId_FingerprintHash_BucketStartUtc"
                ON "CatalogViews" ("CatalogId", "FingerprintHash", "BucketStartUtc");
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_CatalogViews_OwnerUserId"
                ON "CatalogViews" ("OwnerUserId");
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_CatalogViews_CatalogId_ViewedAtUtc"
                ON "CatalogViews" ("CatalogId", "ViewedAtUtc");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_CatalogViews_CatalogId_ViewedAtUtc";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_CatalogViews_OwnerUserId";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_CatalogViews_CatalogId_FingerprintHash_BucketStartUtc";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "CatalogViews";""");
        }
    }
}
