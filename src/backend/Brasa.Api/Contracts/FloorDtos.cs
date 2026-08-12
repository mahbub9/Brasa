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

/// <summary>A room with its tables, as returned to clients.</summary>
public sealed record RoomDto(Guid Id, string Name, int DisplayOrder, IReadOnlyList<TableDto> Tables);

/// <summary>Request body to create a room (FLR-03's room-CRUD follow-up).</summary>
public sealed record CreateRoomRequest(string Name, int DisplayOrder);

/// <summary>Request body to rename or reorder a room (FLR-03's room-CRUD follow-up).</summary>
public sealed record UpdateRoomRequest(string Name, int DisplayOrder);

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
}
