using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Identity.Persistence.Migrations
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
            migrationBuilder.EnableSystemReadFor("organizations", "identity");
            migrationBuilder.EnableSystemReadFor("sites", "identity");
            migrationBuilder.EnableSystemReadFor("terminals", "identity");
            migrationBuilder.EnableSystemReadFor("staff", "identity");
            migrationBuilder.EnableSystemReadFor("feature_flags", "identity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableSystemReadFor("feature_flags", "identity");
            migrationBuilder.DisableSystemReadFor("staff", "identity");
            migrationBuilder.DisableSystemReadFor("terminals", "identity");
            migrationBuilder.DisableSystemReadFor("sites", "identity");
            migrationBuilder.DisableSystemReadFor("organizations", "identity");
        }
    }
}
