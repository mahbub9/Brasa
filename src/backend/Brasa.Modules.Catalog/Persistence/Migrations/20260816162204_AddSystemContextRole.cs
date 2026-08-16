using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// DAT-07 — grants brasa_system read-only, cross-tenant access to every
    /// table this module enabled RLS on before EnableSystemReadFor existed.
    /// No model change, so no CreateTable/AddColumn here — see
    /// RowLevelSecurity.cs.
    /// </remarks>
    public partial class AddSystemContextRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnableSystemReadFor("menu_categories", "catalog");
            migrationBuilder.EnableSystemReadFor("menu_items", "catalog");
            migrationBuilder.EnableSystemReadFor("modifier_groups", "catalog");
            migrationBuilder.EnableSystemReadFor("modifiers", "catalog");
            migrationBuilder.EnableSystemReadFor("price_lists", "catalog");
            migrationBuilder.EnableSystemReadFor("price_list_entries", "catalog");
            migrationBuilder.EnableSystemReadFor("combos", "catalog");
            migrationBuilder.EnableSystemReadFor("combo_components", "catalog");
            migrationBuilder.EnableSystemReadFor("tax_rules", "catalog");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableSystemReadFor("tax_rules", "catalog");
            migrationBuilder.DisableSystemReadFor("combo_components", "catalog");
            migrationBuilder.DisableSystemReadFor("combos", "catalog");
            migrationBuilder.DisableSystemReadFor("price_list_entries", "catalog");
            migrationBuilder.DisableSystemReadFor("price_lists", "catalog");
            migrationBuilder.DisableSystemReadFor("modifiers", "catalog");
            migrationBuilder.DisableSystemReadFor("modifier_groups", "catalog");
            migrationBuilder.DisableSystemReadFor("menu_items", "catalog");
            migrationBuilder.DisableSystemReadFor("menu_categories", "catalog");
        }
    }
}
