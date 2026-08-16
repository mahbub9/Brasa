namespace Brasa.Api.IntegrationTests;

/// <summary>
/// Every test class that migrates through a design-time
/// <c>IDesignTimeDbContextFactory</c> (<see cref="TenantIsolationIntegrationTests"/>,
/// <see cref="OrderConcurrencyIntegrationTests"/>) does it the same way: set
/// the process-wide <c>BRASA_MIGRATIONS_CONNECTION</c> environment variable,
/// call <c>MigrateAsync</c>, then clear it. xUnit runs different test
/// classes in parallel by default, and that env var is genuinely
/// process-global — two such classes' <c>InitializeAsync</c> running at the
/// same time can interleave, so one test's migration silently runs against
/// the <em>other</em> test's container. Found exactly this way: adding
/// <see cref="OrderConcurrencyIntegrationTests"/> as a second consumer of
/// this pattern started failing <see cref="TenantIsolationIntegrationTests"/>
/// deterministically, every run, with a schema that had never actually been
/// migrated. Sharing this collection makes xUnit run every class in it
/// sequentially, which is the actual fix — the alternative (a lock around
/// the env-var mutation) would only be needed if these tests genuinely
/// benefited from running in parallel with each other, and a full
/// Postgres-per-class integration test does not.
/// </summary>
[CollectionDefinition("Migrations env var")]
public sealed class MigrationsEnvVarCollection;
