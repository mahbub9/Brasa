using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Brasa.Api.HealthChecks;

/// <summary>
/// Confirms PostgreSQL is actually reachable, as the runtime (<c>brasa_app</c>)
/// role — the same connection every request depends on. OPS-09.
/// </summary>
/// <remarks>
/// A custom ~20-line check instead of a NuGet health-check package: this is
/// the only dependency it needs (<c>Npgsql</c>, already referenced), and a
/// raw <c>SELECT 1</c> is all "is the database reachable" requires. See
/// ADR 0006 for the same reasoning applied to MediatR.
/// </remarks>
public sealed class DatabaseHealthCheck(string connectionString) : IHealthCheck
{
    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1;";
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            return HealthCheckResult.Healthy("PostgreSQL is reachable.");
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException)
        {
            // Only the failure modes a database being down actually produces
            // are swallowed into a health result — anything else (a bug in
            // this check itself) should still surface as an unhandled
            // exception rather than a quietly wrong "unhealthy".
            return HealthCheckResult.Unhealthy("PostgreSQL is not reachable.", ex);
        }
    }
}
