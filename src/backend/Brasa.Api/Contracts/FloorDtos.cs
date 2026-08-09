using Brasa.Modules.Floor.Domain;

namespace Brasa.Api.Contracts;

/// <summary>A table, as returned to clients.</summary>
public sealed record TableDto(
    Guid Id,
    Guid RoomId,
    string Label,
    int Seats,
    int PositionX,
    int PositionY,
    string Shape,
    string State);

/// <summary>A room with its tables, as returned to clients.</summary>
public sealed record RoomDto(Guid Id, string Name, int DisplayOrder, IReadOnlyList<TableDto> Tables);

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
        table.State.ToString());
}
