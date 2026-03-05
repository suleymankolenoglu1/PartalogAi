using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260301110000_AddPublicAccessLinks")]
    public partial class AddPublicAccessLinks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "PublicAccessLinks" (
                    "Id" uuid NOT NULL,
                    "TokenHash" text NOT NULL,
                    "UserId" uuid NOT NULL,
                    "PublicLinkVersion" integer NOT NULL,
                    "CatalogIds" text NULL,
                    "ExpiresAtUtc" timestamp with time zone NOT NULL,
                    "IsRevoked" boolean NOT NULL DEFAULT FALSE,
                    "CreatedDate" timestamp with time zone NOT NULL,
                    "UpdatedDate" timestamp with time zone NULL,
                    CONSTRAINT "PK_PublicAccessLinks" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_PublicAccessLinks_Users_UserId"
                        FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_PublicAccessLinks_TokenHash"
                ON "PublicAccessLinks" ("TokenHash");
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_PublicAccessLinks_UserId_ExpiresAtUtc"
                ON "PublicAccessLinks" ("UserId", "ExpiresAtUtc");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "PublicAccessLinks";""");
        }
    }
}
