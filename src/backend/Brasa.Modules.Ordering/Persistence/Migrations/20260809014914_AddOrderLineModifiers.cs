using System;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLineModifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "order_line_modifiers",
                schema: "ordering",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    price_delta_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    price_delta_minor_units = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_line_modifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_order_line_modifiers_order_lines_OrderLineId",
                        column: x => x.OrderLineId,
                        principalSchema: "ordering",
                        principalTable: "order_lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_order_line_modifiers_OrderLineId",
                schema: "ordering",
                table: "order_line_modifiers",
                column: "OrderLineId");

            migrationBuilder.CreateIndex(
                name: "IX_order_line_modifiers_TenantId",
                schema: "ordering",
                table: "order_line_modifiers",
                column: "TenantId");

            migrationBuilder.EnableFor("order_line_modifiers", "ordering");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableFor("order_line_modifiers", "ordering");

            migrationBuilder.DropTable(
                name: "order_line_modifiers",
                schema: "ordering");
        }
    }
}
