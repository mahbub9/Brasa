using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "schedule_days",
                schema: "catalog",
                table: "menu_items",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "schedule_end_time",
                schema: "catalog",
                table: "menu_items",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "schedule_start_time",
                schema: "catalog",
                table: "menu_items",
                type: "time without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "schedule_days",
                schema: "catalog",
                table: "menu_items");

            migrationBuilder.DropColumn(
                name: "schedule_end_time",
                schema: "catalog",
                table: "menu_items");

            migrationBuilder.DropColumn(
                name: "schedule_start_time",
                schema: "catalog",
                table: "menu_items");
        }
    }
}
