using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Floor.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Deliberately empty. <c>xmin</c> is a PostgreSQL system column that
    /// already exists on every row of every table — <c>ALTER TABLE ... ADD
    /// COLUMN xmin</c> (what the scaffolder generated here originally) fails
    /// outright, because Postgres reserves that name and refuses to let you
    /// add a real column called "xmin". This migration exists only so EF's
    /// model snapshot records the new shadow concurrency-token property
    /// (<c>TableConfiguration.cs</c>); there is no DDL to run.
    /// </remarks>
    public partial class AddTableXminConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
