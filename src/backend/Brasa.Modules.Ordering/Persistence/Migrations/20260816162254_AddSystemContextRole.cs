using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Ordering.Persistence.Migrations
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
            migrationBuilder.EnableSystemReadFor("orders", "ordering");
            migrationBuilder.EnableSystemReadFor("order_lines", "ordering");
            migrationBuilder.EnableSystemReadFor("order_line_modifiers", "ordering");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableSystemReadFor("order_line_modifiers", "ordering");
            migrationBuilder.DisableSystemReadFor("order_lines", "ordering");
            migrationBuilder.DisableSystemReadFor("orders", "ordering");
        }
    }
}
