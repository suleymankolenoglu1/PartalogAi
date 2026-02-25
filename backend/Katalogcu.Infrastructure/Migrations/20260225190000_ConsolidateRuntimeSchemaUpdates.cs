using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260225190000_ConsolidateRuntimeSchemaUpdates")]
    public partial class ConsolidateRuntimeSchemaUpdates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Users"
                ADD COLUMN IF NOT EXISTS "PhoneNumber" text NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Orders"
                ADD COLUMN IF NOT EXISTS "IdempotencyKey" character varying(128) NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Orders"
                ADD COLUMN IF NOT EXISTS "OwnerUserId" uuid NULL;
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Orders_IdempotencyKey"
                ON "Orders" ("IdempotencyKey")
                WHERE "IdempotencyKey" IS NOT NULL AND "IdempotencyKey" <> '';
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_Orders_OwnerUserId"
                ON "Orders" ("OwnerUserId");
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "CatalogAiJobs" (
                    "Id" uuid NOT NULL,
                    "CreatedDate" timestamp with time zone NOT NULL,
                    "UpdatedDate" timestamp with time zone NULL,
                    "CatalogId" uuid NOT NULL,
                    "Status" character varying(32) NOT NULL,
                    "AttemptCount" integer NOT NULL DEFAULT 0,
                    "MaxAttempts" integer NOT NULL DEFAULT 3,
                    "NextAttemptAt" timestamp with time zone NOT NULL,
                    "LastAttemptAt" timestamp with time zone NULL,
                    "LockedUntil" timestamp with time zone NULL,
                    "LastError" character varying(2048) NULL,
                    CONSTRAINT "PK_CatalogAiJobs" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_CatalogAiJobs_Catalogs_CatalogId"
                        FOREIGN KEY ("CatalogId") REFERENCES "Catalogs" ("Id") ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_CatalogAiJobs_CatalogId"
                ON "CatalogAiJobs" ("CatalogId");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_CatalogAiJobs_Status_NextAttemptAt"
                ON "CatalogAiJobs" ("Status", "NextAttemptAt");
                """);

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "StockMovements" (
                    "Id" uuid NOT NULL,
                    "CreatedDate" timestamp with time zone NOT NULL,
                    "UpdatedDate" timestamp with time zone NULL,
                    "UserId" uuid NOT NULL,
                    "ProductId" uuid NOT NULL,
                    "ProductCode" character varying(128) NOT NULL,
                    "ProductName" character varying(512) NOT NULL,
                    "PreviousQuantity" integer NOT NULL,
                    "DeltaQuantity" integer NOT NULL,
                    "NewQuantity" integer NOT NULL,
                    "MovementType" character varying(32) NOT NULL,
                    "Reason" character varying(1024) NOT NULL,
                    "Source" character varying(128) NULL,
                    "ActorName" character varying(256) NULL,
                    "ReferenceId" character varying(128) NULL,
                    CONSTRAINT "PK_StockMovements" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_StockMovements_Products_ProductId"
                        FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_StockMovements_UserId_CreatedDate"
                ON "StockMovements" ("UserId", "CreatedDate");
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_StockMovements_ProductId_CreatedDate"
                ON "StockMovements" ("ProductId", "CreatedDate");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_StockMovements_ProductId_CreatedDate";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_StockMovements_UserId_CreatedDate";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "StockMovements";""");

            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_CatalogAiJobs_Status_NextAttemptAt";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_CatalogAiJobs_CatalogId";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "CatalogAiJobs";""");

            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Orders_OwnerUserId";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Orders_IdempotencyKey";""");

            migrationBuilder.Sql("""
                ALTER TABLE "Orders"
                DROP COLUMN IF EXISTS "OwnerUserId";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Orders"
                DROP COLUMN IF EXISTS "IdempotencyKey";
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "Users"
                DROP COLUMN IF EXISTS "PhoneNumber";
                """);
        }
    }
}
