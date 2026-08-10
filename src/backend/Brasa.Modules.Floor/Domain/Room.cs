using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;

namespace Brasa.Modules.Floor.Domain;

/// <summary>An area of the restaurant, e.g. "Salão" or "Esplanada".</summary>
public sealed class Room : Entity
{
    private Room()
    {
        // EF Core materialisation.
        Name = string.Empty;
    }

    /// <summary>Creates a new room.</summary>
    public Room(string name, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Room name must not be empty.", nameof(name));
        }

        Name = name;
        DisplayOrder = displayOrder;
    }

    /// <summary>Display name, e.g. "Esplanada".</summary>
    public string Name { get; private set; }

    /// <summary>Sort position among rooms. Lower shows first.</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>
    /// Renames or reorders this room (FLR-03's room-CRUD follow-up). Whether
    /// it has tables, or what state they're in, is irrelevant here — unlike
    /// deleting a room (guarded at the API layer against one with tables
    /// still in it, since <c>Room</c> has no navigation to <c>Table</c> to
    /// check from the domain side), nothing about editing a room's own
    /// fields touches its tables.
    /// </summary>
    public Result Update(string name, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(Error.Validation("floor.invalid_room_name", "Room name must not be empty."));
        }

        Name = name.Trim();
        DisplayOrder = displayOrder;
        return Result.Success();
    }
}
