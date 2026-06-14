using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260606214500_AddAiCapacityLeases")]
    /// <inheritdoc />
    public partial class AddAiCapacityLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "AiCapacityLeases" (
                    "Id" uuid NOT NULL,
                    "PoolName" character varying(128) NOT NULL DEFAULT 'api-chat',
                    "PartitionKey" character varying(256) NOT NULL,
                    "InstanceId" character varying(256) NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
                    "ExpiresAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_AiCapacityLeases" PRIMARY KEY ("Id")
                );
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_AiCapacityLeases_ExpiresAt"
                ON "AiCapacityLeases" ("ExpiresAt");
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_AiCapacityLeases_PoolName_ExpiresAt"
                ON "AiCapacityLeases" ("PoolName", "ExpiresAt");
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_AiCapacityLeases_PoolName_PartitionKey_ExpiresAt"
                ON "AiCapacityLeases" ("PoolName", "PartitionKey", "ExpiresAt");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_AiCapacityLeases_PoolName_PartitionKey_ExpiresAt";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_AiCapacityLeases_PoolName_ExpiresAt";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_AiCapacityLeases_ExpiresAt";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "AiCapacityLeases";""");
        }
    }
}
