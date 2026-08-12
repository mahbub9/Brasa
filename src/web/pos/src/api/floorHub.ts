import * as signalR from '@microsoft/signalr';

// API_BASE_URL (client.ts) carries /api/v1 -- the hub sits at the API's
// root, not under the versioned REST surface, since a connection isn't a
// versioned resource (API-16).
const HUB_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5216/api/v1').replace(/\/api\/v1\/?$/, '');

/**
 * Connects to the floor-changed hub (API-16, this codebase's first
 * realtime channel) and calls `onFloorChanged` whenever any table's state
 * changes anywhere. The message itself carries no data -- see the
 * backend's `FloorHub` for why -- `onFloorChanged` is expected to
 * re-fetch `GET /floor`, the REST equivalent hard rule 7 requires
 * (API-17), rather than trust a push payload.
 */
export function connectFloorHub(onFloorChanged: () => void): signalR.HubConnection {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${HUB_BASE_URL}/hubs/floor`, { withCredentials: false })
    .withAutomaticReconnect()
    .build();

  connection.on('FloorChanged', onFloorChanged);
  // A reconnect can span a gap where a signal was missed entirely -- catch
  // up once, the same as the initial connection would have.
  connection.onreconnected(onFloorChanged);

  connection.start().catch(() => {
    // Best-effort: a lost realtime connection degrades to "stale until the
    // next manual action reloads the floor," not a broken app -- GET
    // /floor is still the source of truth on every explicit action.
  });

  return connection;
}
