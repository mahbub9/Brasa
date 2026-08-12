using System;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "price_lists",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_lists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "price_list_entries",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceListId = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    price_minor_units = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_list_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_price_list_entries_price_lists_PriceListId",
                        column: x => x.PriceListId,
                        principalSchema: "catalog",
                        principalTable: "price_lists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_price_list_entries_PriceListId_MenuItemId",
                schema: "catalog",
                table: "price_list_entries",
                columns: new[] { "PriceListId", "MenuItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_price_list_entries_TenantId",
                schema: "catalog",
                table: "price_list_entries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_price_lists_SiteId",
                schema: "catalog",
                table: "price_lists",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_price_lists_TenantId",
                schema: "catalog",
                table: "price_lists",
                column: "TenantId");

            migrationBuilder.EnableFor("price_lists", "catalog");
            migrationBuilder.EnableFor("price_list_entries", "catalog");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableFor("price_lists", "catalog");
            migrationBuilder.DisableFor("price_list_entries", "catalog");

            migrationBuilder.DropTable(
                name: "price_list_entries",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "price_lists",
                schema: "catalog");
        }
    }
}
