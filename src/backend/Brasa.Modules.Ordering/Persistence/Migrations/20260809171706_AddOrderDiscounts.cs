using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderDiscounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscountKind",
                schema: "ordering",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                schema: "ordering",
                table: "orders",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountKind",
                schema: "ordering",
                table: "order_lines",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                schema: "ordering",
                table: "order_lines",
                type: "numeric(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountKind",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DiscountKind",
                schema: "ordering",
                table: "order_lines");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                schema: "ordering",
                table: "order_lines");
        }
    }
}
