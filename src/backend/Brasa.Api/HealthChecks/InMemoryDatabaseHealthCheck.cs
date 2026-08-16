using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Brasa.Api.HealthChecks;

/// <summary>
/// Swapped in for <see cref="DatabaseHealthCheck"/> when
/// <c>Database:Provider</c> is <c>InMemory</c> (ADR 0012) — there is no
/// external database to probe, so this always reports healthy. Kept as its
/// own check (same <c>"ready"</c> tag) rather than removing the check
/// entirely, so <c>/health/ready</c>'s shape is unchanged for anything
/// polling it.
/// </summary>
public sealed class InMemoryDatabaseHealthCheck : IHealthCheck
{
    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult(HealthCheckResult.Healthy("Using in-memory beta store — no external database."));
}
