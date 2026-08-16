namespace Brasa.Shared.Persistence;

/// <summary>Which store every module's <see cref="Microsoft.EntityFrameworkCore.DbContext"/> talks to.</summary>
/// <remarks>
/// <see cref="Postgres"/> is the only option with a row-level security
/// boundary (<c>docs/architecture/multi-tenancy.md</c>) and the only one
/// permitted in Production — see the guard in <c>Program.cs</c>.
/// <see cref="InMemory"/> exists for a single-tenant pilot deployment with no
/// PostgreSQL to install or babysit on-site. It is backed by SQLite's
/// <c>:memory:</c> mode, not EF Core's own InMemory provider — that
/// provider does not reliably support this codebase's required
/// <c>ComplexProperty</c> <c>Money</c> mapping (confirmed via a real
/// <c>KeyNotFoundException</c> on <c>MenuItem.Price</c>, a known class of
/// EF Core InMemory limitation, not a hypothetical one). SQLite is a real
/// relational engine, so the same mapping that works against PostgreSQL
/// works against it unchanged — see
/// <c>docs/architecture/decisions/0012-beta-in-memory-database.md</c>.
/// </remarks>
public enum DatabaseProvider
{
    Postgres,
    InMemory,
}

/// <summary>Binds the <c>"Database"</c> configuration section.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Defaults to <see cref="DatabaseProvider.Postgres"/> so nothing changes unless explicitly overridden.</summary>
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.Postgres;

    /// <summary>
    /// Whether the dev/demo seeders run at startup. Matches today's behavior
    /// (always on) by default. A beta pilot flips this to <see langword="false"/>
    /// once real menu/floor/staff data has been entered, so a mid-service
    /// restart of the (wiped-on-restart) in-memory store shows an empty state
    /// to re-populate rather than silently reseeding demo placeholders over
    /// what staff actually entered.
    /// </summary>
    public bool SeedOnStartup { get; set; } = true;
}
