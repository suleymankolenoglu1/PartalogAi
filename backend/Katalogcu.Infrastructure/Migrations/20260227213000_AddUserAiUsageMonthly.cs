using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260227213000_AddUserAiUsageMonthly")]
    public partial class AddUserAiUsageMonthly : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS "UserAiUsageMonthly" (
                    "UserId" uuid NOT NULL,
                    "MonthStartUtc" timestamp with time zone NOT NULL,
                    "QueryCount" integer NOT NULL DEFAULT 0,
                    "CreatedDate" timestamp with time zone NOT NULL,
                    "UpdatedDate" timestamp with time zone NULL,
                    CONSTRAINT "PK_UserAiUsageMonthly" PRIMARY KEY ("UserId", "MonthStartUtc"),
                    CONSTRAINT "FK_UserAiUsageMonthly_Users_UserId"
                        FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_UserAiUsageMonthly_MonthStartUtc"
                ON "UserAiUsageMonthly" ("MonthStartUtc");
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TABLE IF EXISTS "UserAiUsageMonthly";""");
        }
    }
}
