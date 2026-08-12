using System;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tax_rules",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IsAlcoholic = table.Column<bool>(type: "boolean", nullable: false),
                    IsTakeaway = table.Column<bool>(type: "boolean", nullable: false),
                    Region = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(4,2)", nullable: false),
                    EffectiveFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveToUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tax_rules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tax_rules_IsAlcoholic_IsTakeaway_Region",
                schema: "catalog",
                table: "tax_rules",
                columns: new[] { "IsAlcoholic", "IsTakeaway", "Region" });

            migrationBuilder.CreateIndex(
                name: "IX_tax_rules_TenantId",
                schema: "catalog",
                table: "tax_rules",
                column: "TenantId");

            migrationBuilder.EnableFor("tax_rules", "catalog");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableFor("tax_rules", "catalog");

            migrationBuilder.DropTable(
                name: "tax_rules",
                schema: "catalog");
        }
    }
}
