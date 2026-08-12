using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemScheduledPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "scheduled_price_currency",
                schema: "catalog",
                table: "menu_items",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "scheduled_price_effective_from_utc",
                schema: "catalog",
                table: "menu_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "scheduled_price_minor_units",
                schema: "catalog",
                table: "menu_items",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "scheduled_price_currency",
                schema: "catalog",
                table: "menu_items");

            migrationBuilder.DropColumn(
                name: "scheduled_price_effective_from_utc",
                schema: "catalog",
                table: "menu_items");

            migrationBuilder.DropColumn(
                name: "scheduled_price_minor_units",
                schema: "catalog",
                table: "menu_items");
        }
    }
}
