using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260304150000_AddPublicStorefrontViews")]
    public partial class AddPublicStorefrontViews : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "PublicStorefrontViews" (
                    "Id" uuid NOT NULL,
                    "OwnerUserId" uuid NOT NULL,
                    "FingerprintHash" text NOT NULL,
                    "BucketStartUtc" timestamp with time zone NOT NULL,
                    "ViewedAtUtc" timestamp with time zone NOT NULL,
                    "Source" text NOT NULL,
                    "CreatedDate" timestamp with time zone NOT NULL,
                    "UpdatedDate" timestamp with time zone NULL,
                    CONSTRAINT "PK_PublicStorefrontViews" PRIMARY KEY ("Id")
                );
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_PublicStorefrontViews_OwnerUserId_FingerprintHash_BucketStartUtc"
                ON "PublicStorefrontViews" ("OwnerUserId", "FingerprintHash", "BucketStartUtc");
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_PublicStorefrontViews_OwnerUserId"
                ON "PublicStorefrontViews" ("OwnerUserId");
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_PublicStorefrontViews_OwnerUserId_ViewedAtUtc"
                ON "PublicStorefrontViews" ("OwnerUserId", "ViewedAtUtc");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_PublicStorefrontViews_OwnerUserId_ViewedAtUtc";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_PublicStorefrontViews_OwnerUserId";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_PublicStorefrontViews_OwnerUserId_FingerprintHash_BucketStartUtc";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "PublicStorefrontViews";""");
        }
    }
}
