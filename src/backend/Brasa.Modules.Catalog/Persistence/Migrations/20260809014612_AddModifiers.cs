using System;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModifiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "modifier_groups",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    MinSelect = table.Column<int>(type: "integer", nullable: false),
                    MaxSelect = table.Column<int>(type: "integer", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_modifier_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_modifier_groups_menu_items_MenuItemId",
                        column: x => x.MenuItemId,
                        principalSchema: "catalog",
                        principalTable: "menu_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "modifiers",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModifierGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_modifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_modifiers_modifier_groups_ModifierGroupId",
                        column: x => x.ModifierGroupId,
                        principalSchema: "catalog",
                        principalTable: "modifier_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_modifier_groups_MenuItemId",
                schema: "catalog",
                table: "modifier_groups",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_modifier_groups_TenantId",
                schema: "catalog",
                table: "modifier_groups",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_modifiers_ModifierGroupId",
                schema: "catalog",
                table: "modifiers",
                column: "ModifierGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_modifiers_TenantId",
                schema: "catalog",
                table: "modifiers",
                column: "TenantId");

            migrationBuilder.EnableFor("modifier_groups", "catalog");
            migrationBuilder.EnableFor("modifiers", "catalog");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableFor("modifier_groups", "catalog");
            migrationBuilder.DisableFor("modifiers", "catalog");

            migrationBuilder.DropTable(
                name: "modifiers",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "modifier_groups",
                schema: "catalog");
        }
    }
}
