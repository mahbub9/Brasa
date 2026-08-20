using Brasa.Shared.Tenancy;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brasa.Shared.Persistence;

/// <summary>
/// The one place every module's <c>AddXModule</c> branches on
/// <see cref="DatabaseProvider"/>, so the branch exists once rather than
/// once per module. See <c>docs/architecture/decisions/0012-beta-in-memory-database.md</c>.
/// </summary>
public static class ModulePersistenceExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TContext"/> against whichever provider
    /// <paramref name="databaseOptions"/> selects.
    /// </summary>
    /// <param name="connectionString">
    /// Required when <see cref="DatabaseOptions.Provider"/> is
    /// <see cref="DatabaseProvider.Postgres"/>; ignored otherwise.
    /// </param>
    /// <param name="schema">
    /// The module's PostgreSQL schema name, reused as the InMemory store's
    /// per-module partition key so each module still gets its own isolated
    /// store, mirroring separate schemas.
    /// </param>
    /// <param name="systemConnectionString">
    /// The <c>brasa_system</c> connection string (DAT-07). Required when
    /// <see cref="DatabaseOptions.Provider"/> is
    /// <see cref="DatabaseProvider.Postgres"/>; ignored otherwise — InMemory
    /// has no RLS-backed system role to connect as, the same accepted
    /// trade-down <see cref="DatabaseProvider.InMemory"/>'s own remarks
    /// already make for the ordinary tenant RLS boundary.
    /// </param>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        DatabaseOptions databaseOptions,
        string? connectionString,
        string schema,
        string? systemConnectionString = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(databaseOptions);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        if (databaseOptions.Provider == DatabaseProvider.InMemory)
        {
            // SQLite's `:memory:` mode discards everything the moment its
            // last open connection closes, so the store needs at least one
            // connection held open for the app's lifetime. The first
            // version of this method did that by opening a single
            // SqliteConnection and reusing that *same connection object*
            // for every DbContext instance — which turned out to be a real
            // concurrency bug, not just an odd shortcut: Microsoft.Data.Sqlite
            // does not support two commands executing concurrently on one
            // connection from different threads, and every DbContext
            // normally owns its own connection for exactly this reason.
            // Two requests landing on the same module at once (e.g.
            // admin's Cash Sessions screen, which fires GetTerminals and
            // GetStaff in parallel) could race on the shared connection and
            // surface as an intermittent, unhandled-exception 500 — the
            // generic "An error occurred while processing your request."
            // ProblemDetails title, caught live in both the Cash Sessions
            // and Feature Flags admin screens.
            //
            // Fixed with SQLite's own named shared-cache technique instead:
            // `cache=shared` lets independent connections opened against
            // the same in-memory database name see the same data, so each
            // DbContext instance goes back to owning its own connection
            // (the Postgres path's normal shape) while `anchorConnection`
            // below just keeps that named database alive between requests.
            // `DefaultTimeout` sets SQLite's busy-timeout so two genuinely
            // concurrent writers block-and-retry for a few seconds instead
            // of failing immediately with "database is locked" — the same
            // trade a real single-file SQLite database would need anyway.
            var inMemoryConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = schema,
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
                DefaultTimeout = 5,
            }.ToString();

            var anchorConnection = new SqliteConnection(inMemoryConnectionString);
            anchorConnection.Open();

            // Nothing above roots this object — the AddDbContext factory
            // below only captures the connection *string*, not this
            // connection instance. Without a live reference kept somewhere,
            // `anchorConnection` is unreachable the moment this method
            // returns and the GC is free to collect and finalize it at any
            // point, closing the native handle and destroying the shared
            // in-memory database out from under whatever schema/data was
            // just written to it — a real bug caught live: EnsureCreatedAsync
            // would create a module's tables, then a GC pass moments later
            // (well within the same startup, before the seeder even ran)
            // would silently wipe them, surfacing as "no such table" on the
            // very next query. Registering the instance as a DI singleton
            // gives the app's own ServiceProvider a live reference for the
            // whole process lifetime — and disposes it cleanly on shutdown
            // too, closing the ADR's own "never explicitly disposed" gap.
            services.AddSingleton(anchorConnection);

            services.AddDbContext<TContext>((_, options) => options.UseSqlite(inMemoryConnectionString));
            // TenantSessionInterceptor deliberately not attached — its
            // set_config() call targets a PostgreSQL session variable that
            // means nothing without RLS. Tenant isolation for InMemory rides
            // entirely on the EF query filter already applied in
            // TenantAwareDbContext.OnModelCreating.
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            ArgumentException.ThrowIfNullOrWhiteSpace(systemConnectionString);

            services.AddDbContext<TContext>((sp, options) =>
            {
                // The setupAction overload of AddDbContext receives the
                // request/scope-local IServiceProvider, not the root one — so
                // resolving the scoped ITenantContext here picks up whatever
                // this specific scope actually resolved to (an ordinary
                // tenant, or ResolveAsSystem() for a background job), not
                // whichever scope happened to build the model first. DAT-07:
                // a system-context scope gets its own connection string,
                // authenticated as brasa_system — a physically separate
                // Npgsql connection pool (Npgsql pools per exact connection
                // string), so an ordinary tenant-scoped request can never
                // end up running on a connection still carrying brasa_system's
                // elevated, cross-tenant privileges, regardless of how
                // connection-pool reset-on-close behaves.
                var tenantContext = sp.GetRequiredService<ITenantContext>();
                var effectiveConnectionString = tenantContext.IsSystemContext
                    ? systemConnectionString
                    : connectionString;

                options.UseNpgsql(
                        effectiveConnectionString,
                        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
                    .AddInterceptors(sp.GetRequiredService<TenantSessionInterceptor>());
            });
        }

        return services;
    }
}
