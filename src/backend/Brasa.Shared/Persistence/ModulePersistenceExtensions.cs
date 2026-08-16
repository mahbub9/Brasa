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
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        DatabaseOptions databaseOptions,
        string? connectionString,
        string schema)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(databaseOptions);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);

        if (databaseOptions.Provider == DatabaseProvider.InMemory)
        {
            // SQLite's `:memory:` mode discards everything the moment its
            // last open connection closes — EF Core's normal per-DbContext
            // connection open/close (one per request scope) would wipe the
            // store between requests. Opening the connection once here,
            // outside the per-scope factory below, and reusing that same
            // open connection object for every DbContext instance keeps
            // the module's data alive for the app's lifetime instead.
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();

            services.AddDbContext<TContext>((_, options) => options.UseSqlite(connection));
            // TenantSessionInterceptor deliberately not attached — its
            // set_config() call targets a PostgreSQL session variable that
            // means nothing without RLS. Tenant isolation for InMemory rides
            // entirely on the EF query filter already applied in
            // TenantAwareDbContext.OnModelCreating.
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            services.AddDbContext<TContext>((sp, options) =>
                options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
                    .AddInterceptors(sp.GetRequiredService<TenantSessionInterceptor>()));
        }

        return services;
    }
}
