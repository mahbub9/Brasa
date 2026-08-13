using System;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "feature_flags",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_flags", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_feature_flags_TenantId",
                schema: "identity",
                table: "feature_flags",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_feature_flags_TenantId_Key_Platform",
                schema: "identity",
                table: "feature_flags",
                columns: new[] { "TenantId", "Key", "Platform" },
                unique: true);

            migrationBuilder.EnableFor("feature_flags", "identity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DisableFor("feature_flags", "identity");

            migrationBuilder.DropTable(
                name: "feature_flags",
                schema: "identity");
        }
    }
}
