using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260526094000_AddCatalogItemSearchText")]
    /// <inheritdoc />
    public partial class AddCatalogItemSearchText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "CatalogItems"
                ADD COLUMN IF NOT EXISTS "SearchText" text;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "CatalogItems"
                ADD COLUMN IF NOT EXISTS "SearchTextVector" tsvector
                GENERATED ALWAYS AS (
                    to_tsvector(
                        'simple',
                        coalesce("SearchText", '') || ' ' ||
                        coalesce("PartName", '') || ' ' ||
                        coalesce("Description", '') || ' ' ||
                        coalesce("PartCode", '') || ' ' ||
                        coalesce("RefNumber", '') || ' ' ||
                        coalesce("MachineBrand", '') || ' ' ||
                        coalesce("MachineModel", '') || ' ' ||
                        coalesce("MachineGroup", '') || ' ' ||
                        coalesce("Mechanism", '') || ' ' ||
                        coalesce("Dimensions", '')
                    )
                ) STORED;
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_CatalogItems_SearchTextVector"
                ON "CatalogItems"
                USING GIN ("SearchTextVector");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_CatalogItems_SearchTextVector";
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "CatalogItems"
                DROP COLUMN IF EXISTS "SearchTextVector";
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "CatalogItems"
                DROP COLUMN IF EXISTS "SearchText";
                """);
        }
    }
}
