using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Floor.Persistence.Migrations
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
            migrationBuilder.EnableSystemReadFor("rooms", "floor");
            migrationBuilder.EnableSystemReadFor("tables", "floor");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableSystemReadFor("tables", "floor");
            migrationBuilder.DisableSystemReadFor("rooms", "floor");
        }
    }
}
