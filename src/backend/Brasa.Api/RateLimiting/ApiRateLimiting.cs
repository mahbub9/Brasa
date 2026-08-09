using Brasa.Api.ClientVersioning;
using Brasa.Shared.Tenancy;

namespace Brasa.Api.RateLimiting;

/// <summary>
/// Partition-key logic for the global <c>/api/**</c> rate limiter (API-12),
/// pulled out of <c>Program.cs</c> so it is unit-testable without a running
/// pipeline or a DI container.
/// </summary>
public static class ApiRateLimiting
{
    /// <summary>
    /// Partition for everything outside <c>/api</c> — health checks, the
    /// OpenAPI document. Not client traffic this exists to protect against,
    /// so it's never limited (see <c>Program.cs</c>'s use of
    /// <c>RateLimitPartition.GetNoLimiter</c> for this key).
    /// </summary>
    public const string UnmeteredPartitionKey = "unmetered";

    /// <summary>
    /// Client id used when the caller sent no (or a malformed)
    /// <c>X-Brasa-Client</c> header — grouped into one shared partition
    /// rather than left unlimited, the same "best-effort, never blocking"
    /// posture <see cref="ClientVersionMiddleware"/> already takes toward a
    /// missing header.
    /// </summary>
    public const string UnknownClientId = "unknown";

    /// <summary>
    /// Resolves which rate-limit bucket a request falls into:
    /// <c>{tenantId:N}:{clientId}</c> for <c>/api/**</c>, or
    /// <see cref="UnmeteredPartitionKey"/> for everything else.
    /// </summary>
    /// <remarks>
    /// Coarser than the ideal "per terminal" key today, because nothing
    /// upstream of authentication (IDN-03…08) identifies a terminal yet —
    /// every <c>pos-web</c> client in a tenant shares one bucket until then.
    /// That's a real, known limitation, not a design goal: revisit once
    /// <see cref="ITenantContext.TerminalId"/> is actually populated.
    /// </remarks>
    public static string ResolvePartitionKey(HttpContext context, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantContext);

        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.Ordinal))
        {
            return UnmeteredPartitionKey;
        }

        var clientId = context.Items[ClientVersionMiddleware.HttpContextItemKey] is ClientInfo info
            ? info.ClientId
            : UnknownClientId;

        return $"{tenantContext.TenantId:N}:{clientId}";
    }
}
