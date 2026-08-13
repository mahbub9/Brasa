using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLineCourseAndFiring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Course",
                schema: "ordering",
                table: "order_lines",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FiredAtUtc",
                schema: "ordering",
                table: "order_lines",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFired",
                schema: "ordering",
                table: "order_lines",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Course",
                schema: "ordering",
                table: "order_lines");

            migrationBuilder.DropColumn(
                name: "FiredAtUtc",
                schema: "ordering",
                table: "order_lines");

            migrationBuilder.DropColumn(
                name: "IsFired",
                schema: "ordering",
                table: "order_lines");
        }
    }
}
