using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Floor.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTableGroupId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                schema: "floor",
                table: "tables",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tables_GroupId",
                schema: "floor",
                table: "tables",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tables_GroupId",
                schema: "floor",
                table: "tables");

            migrationBuilder.DropColumn(
                name: "GroupId",
                schema: "floor",
                table: "tables");
        }
    }
}
