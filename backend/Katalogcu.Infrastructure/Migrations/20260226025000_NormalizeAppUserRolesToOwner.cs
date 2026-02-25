using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260226025000_NormalizeAppUserRolesToOwner")]
    public partial class NormalizeAppUserRolesToOwner : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "Users"
                SET "Role" = 'Owner'
                WHERE "Role" IS NULL
                   OR btrim("Role") = ''
                   OR lower("Role") = 'customer';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: kullanıcı rolünü geri çevirmek veri kaybı riski taşır.
        }
    }
}
