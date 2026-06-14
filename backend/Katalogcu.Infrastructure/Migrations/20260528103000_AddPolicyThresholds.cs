using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260528103000_AddPolicyThresholds")]
    /// <inheritdoc />
    public partial class AddPolicyThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "PolicyThresholds" (
                    "Id" uuid NOT NULL,
                    "ScopeType" character varying(32) NOT NULL,
                    "ScopeKey" character varying(128) NOT NULL,
                    "HighConfidence" numeric(5,4),
                    "LowConfidence" numeric(5,4),
                    "AmbiguityScoreDelta" numeric(5,4),
                    "IsActive" boolean NOT NULL DEFAULT TRUE,
                    "Version" integer NOT NULL DEFAULT 1,
                    "Notes" character varying(1024),
                    "UpdatedBy" character varying(256),
                    "CreatedDate" timestamp with time zone NOT NULL DEFAULT now(),
                    "UpdatedDate" timestamp with time zone,
                    CONSTRAINT "PK_PolicyThresholds" PRIMARY KEY ("Id"),
                    CONSTRAINT "CK_PolicyThresholds_ScopeType"
                        CHECK ("ScopeType" IN ('Global', 'Brand', 'Catalog')),
                    CONSTRAINT "CK_PolicyThresholds_HasThreshold"
                        CHECK (
                            "HighConfidence" IS NOT NULL
                            OR "LowConfidence" IS NOT NULL
                            OR "AmbiguityScoreDelta" IS NOT NULL
                        ),
                    CONSTRAINT "CK_PolicyThresholds_HighConfidence_Range"
                        CHECK ("HighConfidence" IS NULL OR ("HighConfidence" >= 0 AND "HighConfidence" <= 1)),
                    CONSTRAINT "CK_PolicyThresholds_LowConfidence_Range"
                        CHECK ("LowConfidence" IS NULL OR ("LowConfidence" >= 0 AND "LowConfidence" <= 1)),
                    CONSTRAINT "CK_PolicyThresholds_AmbiguityScoreDelta_Range"
                        CHECK ("AmbiguityScoreDelta" IS NULL OR ("AmbiguityScoreDelta" >= 0 AND "AmbiguityScoreDelta" <= 1))
                );
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "UX_PolicyThresholds_ActiveScope"
                ON "PolicyThresholds" ("ScopeType", "ScopeKey")
                WHERE "IsActive" = TRUE;
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_PolicyThresholds_ActiveScope"
                ON "PolicyThresholds" ("IsActive", "ScopeType", "ScopeKey");
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "PolicyThresholds" (
                    "Id",
                    "ScopeType",
                    "ScopeKey",
                    "HighConfidence",
                    "LowConfidence",
                    "AmbiguityScoreDelta",
                    "IsActive",
                    "Version",
                    "Notes",
                    "CreatedDate"
                )
                VALUES (
                    '00000000-0000-0000-0000-000000000001',
                    'Global',
                    'default',
                    0.8500,
                    0.5500,
                    0.1000,
                    TRUE,
                    1,
                    'Default confidence gate policy',
                    now()
                )
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_PolicyThresholds_ActiveScope";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "UX_PolicyThresholds_ActiveScope";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "PolicyThresholds";""");
        }
    }
}
