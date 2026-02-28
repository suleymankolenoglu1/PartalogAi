using System;
using Katalogcu.Domain.Enums;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Katalogcu.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260227183000_AddSubscriptionPlanToUsers")]
    public partial class AddSubscriptionPlanToUsers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxCatalogCount",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "MaxPagePerCatalog",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlanActivatedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlanExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubscriptionPlan",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: (int)SubscriptionPlan.CatalogOnly);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxCatalogCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MaxPagePerCatalog",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PlanActivatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PlanExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SubscriptionPlan",
                table: "Users");
        }
    }
}
