using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Payments.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashSessionBlindCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CountedAtUtc",
                schema: "payments",
                table: "cash_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CountedByStaffId",
                schema: "payments",
                table: "cash_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "counted_amount_currency",
                schema: "payments",
                table: "cash_sessions",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "counted_amount_minor_units",
                schema: "payments",
                table: "cash_sessions",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountedAtUtc",
                schema: "payments",
                table: "cash_sessions");

            migrationBuilder.DropColumn(
                name: "CountedByStaffId",
                schema: "payments",
                table: "cash_sessions");

            migrationBuilder.DropColumn(
                name: "counted_amount_currency",
                schema: "payments",
                table: "cash_sessions");

            migrationBuilder.DropColumn(
                name: "counted_amount_minor_units",
                schema: "payments",
                table: "cash_sessions");
        }
    }
}
