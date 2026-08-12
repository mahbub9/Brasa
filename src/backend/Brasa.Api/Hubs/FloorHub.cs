using Microsoft.AspNetCore.SignalR;

namespace Brasa.Api.Hubs;

/// <summary>
/// Pushes a payload-less signal whenever any table's state changes (API-16
/// — this codebase's first realtime channel). Broadcast-only: no
/// server-invokable client methods, and the message itself carries no
/// data. A client that receives it re-fetches <c>GET /floor</c> — the REST
/// equivalent hard rule 7 requires every realtime message to have (API-17)
/// — rather than trusting a partial, possibly-stale push payload. That
/// also means there is nothing here to get out of sync with the DTO shape
/// as Floor's own contract evolves.
/// </summary>
/// <remarks>
/// No per-tenant/per-terminal targeting yet — every connected client is in
/// <see cref="Clients"/>' <c>All</c> group, the broadcast-to-everyone shape
/// that already matches how <c>DevTenantMiddleware</c> resolves a single
/// dev tenant with no real multi-tenant routing in front of it. Scoping
/// broadcasts to one tenant's own connections is real, deferred work for
/// whenever IDN-06/07 (terminal identity) exists to key a SignalR group by.
/// </remarks>
public sealed class FloorHub : Hub
{
}

/// <summary>Broadcasts a floor-changed signal (API-16) with one shared, typo-proof method name.</summary>
public static class FloorHubBroadcast
{
    /// <summary>The client-side event name every <see cref="FloorHub"/> broadcast uses.</summary>
    public const string FloorChangedMethod = "FloorChanged";

    /// <summary>Tells every connected client the floor changed — see <see cref="FloorHub"/>'s own remarks for why the message itself carries no data.</summary>
    public static Task NotifyFloorChangedAsync(this IHubContext<FloorHub> hub, CancellationToken cancellationToken)
        => hub.Clients.All.SendAsync(FloorChangedMethod, cancellationToken);
}
