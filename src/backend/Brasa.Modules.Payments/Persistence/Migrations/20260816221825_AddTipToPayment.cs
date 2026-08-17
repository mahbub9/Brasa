using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Payments.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTipToPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AttributedStaffId",
                schema: "payments",
                table: "payments",
                type: "uuid",
                nullable: true);

            // Default "Eur", not "" — this column's string conversion stores
            // the CurrencyCode enum's member name (CurrencyCode.Eur = 0, the
            // only currency this system has today), and any already-existing
            // row (this table is not new — PAY-01/02/05 already wrote real
            // rows before this migration existed) must deserialize back to a
            // valid CurrencyCode, not throw a FormatException on next read.
            migrationBuilder.AddColumn<string>(
                name: "tip_amount_currency",
                schema: "payments",
                table: "payments",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "Eur");

            migrationBuilder.AddColumn<long>(
                name: "tip_amount_minor_units",
                schema: "payments",
                table: "payments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttributedStaffId",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "tip_amount_currency",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "tip_amount_minor_units",
                schema: "payments",
                table: "payments");
        }
    }
}
