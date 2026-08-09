using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemTakeawayPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "takeaway_price_currency",
                schema: "catalog",
                table: "menu_items",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "takeaway_price_minor_units",
                schema: "catalog",
                table: "menu_items",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "takeaway_price_currency",
                schema: "catalog",
                table: "menu_items");

            migrationBuilder.DropColumn(
                name: "takeaway_price_minor_units",
                schema: "catalog",
                table: "menu_items");
        }
    }
}
