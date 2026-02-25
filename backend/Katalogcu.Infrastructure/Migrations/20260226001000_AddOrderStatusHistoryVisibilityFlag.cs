using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260226001000_AddOrderStatusHistoryVisibilityFlag")]
    public partial class AddOrderStatusHistoryVisibilityFlag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS "OrderStatusHistory"
                ADD COLUMN IF NOT EXISTS "IsVisibleToCustomer" boolean NOT NULL DEFAULT TRUE;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE IF EXISTS "OrderStatusHistory"
                DROP COLUMN IF EXISTS "IsVisibleToCustomer";
                """);
        }
    }
}
