using System;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Floor.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "floor");

            migrationBuilder.CreateTable(
                name: "rooms",
                schema: "floor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tables",
                schema: "floor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Seats = table.Column<int>(type: "integer", nullable: false),
                    PositionX = table.Column<int>(type: "integer", nullable: false),
                    PositionY = table.Column<int>(type: "integer", nullable: false),
                    Shape = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tables", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rooms_TenantId",
                schema: "floor",
                table: "rooms",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_tables_RoomId",
                schema: "floor",
                table: "tables",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_tables_State",
                schema: "floor",
                table: "tables",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_tables_TenantId",
                schema: "floor",
                table: "tables",
                column: "TenantId");

            migrationBuilder.EnableFor("rooms", "floor");
            migrationBuilder.EnableFor("tables", "floor");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableFor("rooms", "floor");
            migrationBuilder.DisableFor("tables", "floor");

            migrationBuilder.DropTable(
                name: "rooms",
                schema: "floor");

            migrationBuilder.DropTable(
                name: "tables",
                schema: "floor");
        }
    }
}
