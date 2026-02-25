using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260225233000_AddOrderStatusHistory")]
    public partial class AddOrderStatusHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "OrderStatusHistory" (
                    "Id" uuid NOT NULL,
                    "CreatedDate" timestamp with time zone NOT NULL,
                    "UpdatedDate" timestamp with time zone NULL,
                    "OrderId" uuid NOT NULL,
                    "PreviousStatus" integer NULL,
                    "NewStatus" integer NOT NULL,
                    "IsVisibleToCustomer" boolean NOT NULL DEFAULT TRUE,
                    "Source" character varying(64) NOT NULL,
                    "Note" character varying(512) NULL,
                    "ChangedBy" character varying(256) NULL,
                    CONSTRAINT "PK_OrderStatusHistory" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_OrderStatusHistory_Orders_OrderId"
                        FOREIGN KEY ("OrderId") REFERENCES "Orders" ("Id") ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_OrderStatusHistory_OrderId_CreatedDate"
                ON "OrderStatusHistory" ("OrderId", "CreatedDate");
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "OrderStatusHistory"
                ADD COLUMN IF NOT EXISTS "IsVisibleToCustomer" boolean NOT NULL DEFAULT TRUE;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_OrderStatusHistory_OrderId_CreatedDate";""");
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "OrderStatusHistory";""");
        }
    }
}
