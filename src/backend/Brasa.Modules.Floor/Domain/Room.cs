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
    /// <param name="floorLevel">
    /// Which physical storey this room sits on (FLR-07) — <c>0</c> ground
    /// floor, positive above it, negative below (a basement or cave). Most
    /// restaurants are single-storey, so this defaults to <c>0</c> and both
    /// clients only surface it once a tenant's own rooms actually span more
    /// than one level — see <c>TablePicker</c>/<c>FloorManager</c>'s own
    /// remarks.
    /// </param>
    public Room(string name, int displayOrder, int floorLevel = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Room name must not be empty.", nameof(name));
        }

        Name = name;
        DisplayOrder = displayOrder;
        FloorLevel = floorLevel;
    }

    /// <summary>Display name, e.g. "Esplanada".</summary>
    public string Name { get; private set; }

    /// <summary>Sort position among rooms. Lower shows first.</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>Which physical storey this room sits on (FLR-07). See the constructor's own remarks.</summary>
    public int FloorLevel { get; private set; }

    /// <summary>
    /// The waiter currently assigned to this room as their section (FLR-06)
    /// — <c>null</c> when unassigned. A plain, opaque reference to Identity's
    /// <c>Staff</c>, the same pattern Ordering's own <c>Order.TableId</c>
    /// already uses for a Floor <c>Table</c>: Floor never queries Identity
    /// directly, and this id is never domain-validated here — the API layer
    /// confirms it names a real staff member before ever calling
    /// <see cref="AssignSection"/>.
    /// </summary>
    public Guid? AssignedStaffId { get; private set; }

    /// <summary>
    /// Assigns or clears (<paramref name="staffId"/> <c>null</c>) which
    /// waiter is working this room as their section. No domain invariant to
    /// check — unlike <see cref="Table.JoinGroup"/>, a room has no state of
    /// its own that assignment could conflict with, so this can never fail.
    /// </summary>
    public void AssignSection(Guid? staffId) => AssignedStaffId = staffId;

    /// <summary>
    /// Renames, reorders or moves this room to a different floor (FLR-03's
    /// room-CRUD follow-up; FLR-07 for the floor level). Whether it has
    /// tables, or what state they're in, is irrelevant here — unlike
    /// deleting a room (guarded at the API layer against one with tables
    /// still in it, since <c>Room</c> has no navigation to <c>Table</c> to
    /// check from the domain side), nothing about editing a room's own
    /// fields touches its tables.
    /// </summary>
    public Result Update(string name, int displayOrder, int floorLevel)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(Error.Validation("floor.invalid_room_name", "Room name must not be empty."));
        }

        Name = name.Trim();
        DisplayOrder = displayOrder;
        FloorLevel = floorLevel;
        return Result.Success();
    }
}
