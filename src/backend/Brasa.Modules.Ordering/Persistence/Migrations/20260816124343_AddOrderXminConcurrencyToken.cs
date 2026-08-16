using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brasa.Modules.Ordering.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Deliberately empty, the same trap and the same fix as Floor's own
    /// <c>AddTableXminConcurrencyToken</c>: <c>xmin</c> is a PostgreSQL
    /// system column that already exists on every row of every table —
    /// <c>ALTER TABLE ... ADD COLUMN xmin</c> (what the scaffolder generated
    /// here originally) fails outright, because Postgres reserves that name
    /// and refuses to let you add a real column called "xmin". This
    /// migration exists only so EF's model snapshot records the new shadow
    /// concurrency-token property (<c>OrderConfiguration.cs</c>, ORD-21);
    /// there is no DDL to run.
    /// </remarks>
    public partial class AddOrderXminConcurrencyToken : Migration
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
