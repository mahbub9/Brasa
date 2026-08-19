using System;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Payments.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashMovement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cash_movements",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CashSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RecordedByStaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    amount_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount_minor_units = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_movements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cash_movements_CashSessionId",
                schema: "payments",
                table: "cash_movements",
                column: "CashSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_movements_TenantId",
                schema: "payments",
                table: "cash_movements",
                column: "TenantId");

            // dotnet ef never emits these -- hand-added every time a new
            // table lands. Argument order is (table, schema), NOT
            // (schema, table) -- confirmed against RowLevelSecurity.cs's
            // own signature this time, not by analogy to another
            // migration, after AddCashSession got this backwards once
            // already (see the trap in docs/ai/README.md).
            migrationBuilder.EnableFor("cash_movements", "payments");
            migrationBuilder.EnableSystemReadFor("cash_movements", "payments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableSystemReadFor("cash_movements", "payments");
            migrationBuilder.DisableFor("cash_movements", "payments");

            migrationBuilder.DropTable(
                name: "cash_movements",
                schema: "payments");
        }
    }
}
