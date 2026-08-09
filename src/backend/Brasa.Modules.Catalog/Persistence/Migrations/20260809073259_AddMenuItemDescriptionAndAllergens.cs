using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuItemDescriptionAndAllergens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "catalog",
                table: "menu_items",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "allergens",
                schema: "catalog",
                table: "menu_items",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                schema: "catalog",
                table: "menu_items");

            migrationBuilder.DropColumn(
                name: "allergens",
                schema: "catalog",
                table: "menu_items");
        }
    }
}
