using Brasa.Modules.Floor.Domain;

namespace Brasa.Api.Contracts;

/// <summary>
/// A table, as returned to clients. <c>GroupId</c> is null unless this
/// table is currently pushed together with others as one seating unit
/// (FLR-05) — see <c>Table.GroupId</c>'s own remarks for what that does
/// and does not change.
/// </summary>
public sealed record TableDto(
    Guid Id,
    Guid RoomId,
    string Label,
    int Seats,
    int PositionX,
    int PositionY,
    string Shape,
    string State,
    Guid? GroupId);

/// <summary>
/// A room with its tables, as returned to clients. <c>FloorLevel</c> is
/// which physical storey it sits on (FLR-07) — <c>0</c> ground floor,
/// positive above it, negative below. <c>AssignedStaffId</c>/<c>AssignedStaffName</c>
/// (FLR-06) are null together when the room has no waiter assigned as its
/// section — <c>AssignedStaffName</c> is resolved fresh from Identity on
/// every read, never snapshotted, so a renamed staff member never shows a
/// stale name here.
/// </summary>
public sealed record RoomDto(
    Guid Id,
    string Name,
    int DisplayOrder,
    int FloorLevel,
    Guid? AssignedStaffId,
    string? AssignedStaffName,
    IReadOnlyList<TableDto> Tables);

/// <summary>Request body to create a room (FLR-03's room-CRUD follow-up). <c>FloorLevel</c> defaults to <c>0</c> (FLR-07).</summary>
public sealed record CreateRoomRequest(string Name, int DisplayOrder, int FloorLevel = 0);

/// <summary>Request body to rename, reorder or move a room to a different floor (FLR-03's room-CRUD follow-up; FLR-07).</summary>
public sealed record UpdateRoomRequest(string Name, int DisplayOrder, int FloorLevel);

/// <summary>Request body to assign or clear (<c>StaffId</c> null) a room's section waiter (FLR-06).</summary>
public sealed record AssignRoomSectionRequest(Guid? StaffId);

/// <summary>
/// Request body to add a table to a room (FLR-03). <c>Shape</c> is a
/// <see cref="Domain.TableShape"/> name, e.g. <c>"Round"</c>
/// (case-insensitive), the same string-not-JSON-enum convention every
/// enum-shaped field in this API already uses.
/// </summary>
public sealed record CreateTableRequest(string Label, int Seats, int PositionX, int PositionY, string Shape);

/// <summary>Request body to edit a table's label, seats, shape or position (FLR-03). Same shape as <see cref="CreateTableRequest"/>, minus the room — a table doesn't move between rooms this way.</summary>
public sealed record UpdateTableRequest(string Label, int Seats, int PositionX, int PositionY, string Shape);

/// <summary>
/// Request body to push 2+ free tables together into one seating group
/// (FLR-05).
/// </summary>
public sealed record CreateTableGroupRequest(IReadOnlyList<Guid> TableIds);

/// <summary>Maps Floor domain entities to wire DTOs.</summary>
public static class FloorDtoMappings
{
    /// <summary>Converts a table to its wire representation.</summary>
    public static TableDto ToDto(this Table table) => new(
        table.Id,
        table.RoomId,
        table.Label,
        table.Seats,
        table.PositionX,
        table.PositionY,
        table.Shape.ToString(),
        table.State.ToString(),
        table.GroupId);

    /// <summary>
    /// Converts a room to its wire representation. <paramref name="staffNamesById"/>
    /// is a pre-resolved lookup (FLR-06) — see
    /// <c>FloorEndpoints.ResolveStaffNamesAsync</c> — rather than a per-room
    /// Identity query, so listing every room on <c>GET /floor</c> stays one
    /// batched lookup, not N+1.
    /// </summary>
    public static RoomDto ToDto(
        this Room room, IReadOnlyList<TableDto> tables, IReadOnlyDictionary<Guid, string> staffNamesById) => new(
        room.Id,
        room.Name,
        room.DisplayOrder,
        room.FloorLevel,
        room.AssignedStaffId,
        room.AssignedStaffId is { } staffId && staffNamesById.TryGetValue(staffId, out var name) ? name : null,
        tables);
}
