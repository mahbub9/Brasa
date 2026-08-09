namespace Brasa.Modules.Floor.Domain;

/// <summary>
/// A table's lifecycle across one seating. See <see cref="Table"/> for the
/// transitions between these.
/// </summary>
public enum TableState
{
    /// <summary>No one seated. Can be opened.</summary>
    Free = 0,

    /// <summary>Guests seated, an order is open against it.</summary>
    Occupied = 1,

    /// <summary>Guests have asked for the bill. Still occupied.</summary>
    BillRequested = 2,

    /// <summary>Guests have left; the order is closed but the table needs clearing.</summary>
    Dirty = 3,
}
