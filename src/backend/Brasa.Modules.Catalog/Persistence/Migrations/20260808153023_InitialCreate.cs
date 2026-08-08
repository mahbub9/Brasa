using System;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "menu_categories",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_menu_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "menu_items",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    vat_rate = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    IsAlcoholic = table.Column<bool>(type: "boolean", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_menu_items", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_menu_categories_TenantId",
                schema: "catalog",
                table: "menu_categories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_menu_items_CategoryId",
                schema: "catalog",
                table: "menu_items",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_menu_items_TenantId",
                schema: "catalog",
                table: "menu_items",
                column: "TenantId");

            // DAT-05: row-level security is the real tenant isolation boundary —
            // the EF Core query filter is only a convenience. See
            // docs/architecture/multi-tenancy.md. Added in the same migration
            // that creates the table, never as a follow-up: a table created
            // without a policy is a data leak waiting for its first bug.
            migrationBuilder.EnableFor("menu_categories", "catalog");
            migrationBuilder.EnableFor("menu_items", "catalog");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableFor("menu_categories", "catalog");
            migrationBuilder.DisableFor("menu_items", "catalog");

            migrationBuilder.DropTable(
                name: "menu_categories",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "menu_items",
                schema: "catalog");
        }
    }
}
