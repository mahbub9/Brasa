using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLineNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "ordering",
                table: "order_lines",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "ordering",
                table: "order_lines");
        }
    }
}
