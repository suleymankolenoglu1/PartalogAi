using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260528090000_AddCatalogItemsEmbeddingHnswIndex")]
    /// <inheritdoc />
    public partial class AddCatalogItemsEmbeddingHnswIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_catalog_items_embedding_hnsw
                ON "CatalogItems"
                USING hnsw (("Embedding"::halfvec(3072)) halfvec_cosine_ops)
                WITH (m = 16, ef_construction = 64);
                """,
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX CONCURRENTLY IF EXISTS idx_catalog_items_embedding_hnsw;
                """,
                suppressTransaction: true);
        }
    }
}
