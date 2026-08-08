using System.Data.Common;
using System.Globalization;
using Brasa.Shared.Tenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Brasa.Shared.Persistence;

/// <summary>
/// Sets the PostgreSQL session variable that row-level security policies read,
/// every time a connection is opened.
/// </summary>
/// <remarks>
/// <para>
/// Npgsql resets session state when a connection returns to the pool, so a
/// setting applied here cannot leak into the next request's tenant. That reset
/// is load-bearing: if connection pooling is ever configured with
/// <c>No Reset On Close=true</c>, this becomes a cross-tenant data leak.
/// </para>
/// <para>
/// The value is passed as a parameter to <c>set_config</c> rather than
/// interpolated, so a tenant id can never be SQL.
/// </para>
/// </remarks>
public sealed class TenantSessionInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    /// <inheritdoc/>
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await ApplyAsync(connection, cancellationToken).ConfigureAwait(false);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(connection);

        ApplyAsync(connection, CancellationToken.None).GetAwaiter().GetResult();
        base.ConnectionOpened(connection, eventData);
    }

    private async Task ApplyAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        // No tenant resolved — deliberately leave the setting unset. The policy
        // then matches no rows, so an unauthenticated or misconfigured path sees
        // nothing rather than everything.
        if (!tenantContext.HasTenant)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT set_config('{RowLevelSecurity.TenantSetting}', @tenantId, false)";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenantId";
        parameter.Value = tenantContext.TenantId.ToString("D", CultureInfo.InvariantCulture);
        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
