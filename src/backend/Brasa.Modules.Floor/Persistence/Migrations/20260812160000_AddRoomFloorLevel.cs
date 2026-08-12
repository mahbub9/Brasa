using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Floor.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomFloorLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FloorLevel",
                schema: "floor",
                table: "rooms",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FloorLevel",
                schema: "floor",
                table: "rooms");
        }
    }
}
