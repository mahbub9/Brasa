using System;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Payments.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "payments");

            migrationBuilder.CreateTable(
                name: "payments",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PaidAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    amount_due_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount_due_minor_units = table.Column<long>(type: "bigint", nullable: false),
                    amount_tendered_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount_tendered_minor_units = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payments_OrderId",
                schema: "payments",
                table: "payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_TenantId",
                schema: "payments",
                table: "payments",
                column: "TenantId");

            // dotnet ef never emits these -- hand-added every time a new
            // schema lands, the same trap this codebase already caught once
            // for Identity (IDN-01). A table created without both calls is a
            // data leak (EnableFor) or invisible to background jobs forever
            // (EnableSystemReadFor, DAT-07) -- see RowLevelSecurity.cs's own
            // class remarks for why these stay two separate calls.
            migrationBuilder.EnableFor("payments", "payments");
            migrationBuilder.EnableSystemReadFor("payments", "payments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableSystemReadFor("payments", "payments");
            migrationBuilder.DisableFor("payments", "payments");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "payments");
        }
    }
}
