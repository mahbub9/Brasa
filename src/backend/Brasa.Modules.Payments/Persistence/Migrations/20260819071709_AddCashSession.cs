using System;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Payments.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cash_sessions",
                schema: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TerminalId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedByStaffId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    opening_float_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    opening_float_minor_units = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cash_sessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cash_sessions_TenantId",
                schema: "payments",
                table: "cash_sessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_cash_sessions_TerminalId",
                schema: "payments",
                table: "cash_sessions",
                column: "TerminalId");

            // dotnet ef never emits these -- hand-added every time a new
            // table lands, the same trap this codebase already caught for
            // Identity (IDN-01) and for payments itself (InitialCreate). A
            // table created without both calls is a data leak (EnableFor)
            // or invisible to background jobs forever (EnableSystemReadFor,
            // DAT-07) -- see RowLevelSecurity.cs's own class remarks for why
            // these stay two separate calls.
            migrationBuilder.EnableFor("cash_sessions", "payments");
            migrationBuilder.EnableSystemReadFor("cash_sessions", "payments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableSystemReadFor("cash_sessions", "payments");
            migrationBuilder.DisableFor("cash_sessions", "payments");

            migrationBuilder.DropTable(
                name: "cash_sessions",
                schema: "payments");
        }
    }
}
