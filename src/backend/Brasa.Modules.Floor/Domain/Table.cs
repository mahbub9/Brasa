using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;

namespace Brasa.Modules.Floor.Domain;

/// <summary>
/// A physical table on the floor plan and its current seating state.
/// </summary>
/// <remarks>
/// <para>
/// Position and shape exist now so a future drag-and-drop editor (FLR-03) has
/// somewhere to persist to without a schema change — I1 renders tables from
/// these coordinates in a static grid, not yet an editable canvas.
/// </para>
/// <para>
/// <c>Ordering.Order</c> references a table only by <c>Guid TableId</c> — a
/// plain, opaque reference, the same pattern as <c>OrderLine.MenuItemId</c>.
/// Neither module queries the other's tables directly; the API layer
/// orchestrates both, exactly as it already does for Catalog, Ordering and
/// Fiscal together in <c>OrderEndpoints</c>. See
/// <c>docs/architecture/module-boundaries.md</c>.
/// </para>
/// </remarks>
public sealed class Table : Entity
{
    private Table()
    {
        // EF Core materialisation.
        Label = string.Empty;
    }

    /// <summary>Creates a new table, free by default.</summary>
    public Table(Guid roomId, string label, int seats, int positionX, int positionY, TableShape shape)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("Table label must not be empty.", nameof(label));
        }

        if (seats < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(seats), seats, "Seats must be at least 1.");
        }

        RoomId = roomId;
        Label = label;
        Seats = seats;
        PositionX = positionX;
        PositionY = positionY;
        Shape = shape;
        State = TableState.Free;
    }

    /// <summary>The room this table sits in.</summary>
    public Guid RoomId { get; private set; }

    /// <summary>Display label, e.g. "Mesa 5".</summary>
    public string Label { get; private set; }

    /// <summary>How many guests it seats.</summary>
    public int Seats { get; private set; }

    /// <summary>Horizontal position on the floor plan, in layout units.</summary>
    public int PositionX { get; private set; }

    /// <summary>Vertical position on the floor plan, in layout units.</summary>
    public int PositionY { get; private set; }

    /// <summary>How the table renders.</summary>
    public TableShape Shape { get; private set; }

    /// <summary>Current lifecycle state.</summary>
    public TableState State { get; private set; }

    /// <summary>Seats guests and opens the table for ordering. Requires the table to be free.</summary>
    public Result Occupy()
    {
        if (State != TableState.Free)
        {
            return Result.Failure(Error.Conflict("floor.table_not_free", $"Table {Label} is not free."));
        }

        State = TableState.Occupied;
        return Result.Success();
    }

    /// <summary>Marks that the guests have asked for the bill. Requires the table to be occupied.</summary>
    public Result RequestBill()
    {
        if (State != TableState.Occupied)
        {
            return Result.Failure(Error.Conflict("floor.table_not_occupied", $"Table {Label} is not occupied."));
        }

        State = TableState.BillRequested;
        return Result.Success();
    }

    /// <summary>
    /// Marks the table dirty — guests have left and the order closed, but it
    /// still needs clearing before the next party can be seated.
    /// </summary>
    public Result MarkDirty()
    {
        if (State is not (TableState.Occupied or TableState.BillRequested))
        {
            return Result.Failure(
                Error.Conflict("floor.table_not_occupied", $"Table {Label} is not occupied."));
        }

        State = TableState.Dirty;
        return Result.Success();
    }

    /// <summary>
    /// Frees the table directly, skipping <see cref="TableState.Dirty"/> —
    /// used only when a party transfers to a different table mid-service
    /// (ORD-12), never when they've paid and left. Closing a bill always
    /// goes through <see cref="MarkDirty"/>/<see cref="Clear"/> so staff
    /// still physically reset the table; a transfer skips that because the
    /// party is simply moving, not finishing.
    /// </summary>
    public Result Release()
    {
        if (State is not (TableState.Occupied or TableState.BillRequested))
        {
            return Result.Failure(Error.Conflict("floor.table_not_occupied", $"Table {Label} is not occupied."));
        }

        State = TableState.Free;
        return Result.Success();
    }

    /// <summary>Staff confirms the table has been cleared and reset. Requires the table to be dirty.</summary>
    public Result Clear()
    {
        if (State != TableState.Dirty)
        {
            return Result.Failure(Error.Conflict("floor.table_not_dirty", $"Table {Label} is not dirty."));
        }

        State = TableState.Free;
        return Result.Success();
    }

    /// <summary>
    /// Updates this table's editable floor-plan fields (FLR-03) — label,
    /// seating capacity, shape and position. Independent of
    /// <see cref="State"/>: repositioning or relabelling a table doesn't
    /// require it to be empty first, unlike <see cref="Delete"/>.
    /// </summary>
    public Result Update(string label, int seats, int positionX, int positionY, TableShape shape)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return Result.Failure(Error.Validation("floor.invalid_label", "Table label must not be empty."));
        }

        if (seats < 1)
        {
            return Result.Failure(Error.Validation("floor.invalid_seats", "Seats must be at least 1."));
        }

        Label = label.Trim();
        Seats = seats;
        PositionX = positionX;
        PositionY = positionY;
        Shape = shape;
        return Result.Success();
    }

    /// <summary>
    /// Guards removing this table from the floor plan entirely (FLR-03) —
    /// the actual delete is a plain <c>DbSet.Remove</c> at the API layer,
    /// this only checks it's safe. Requires <see cref="TableState.Free"/>:
    /// a table mid-service, or dirty and awaiting clearing, must not simply
    /// vanish. Safe to hard delete rather than soft-delete like
    /// <c>MenuItem</c> (CAT-18): a closed order's <c>TableLabel</c> is
    /// already snapshotted at open time (<c>Order.TableLabel</c>), so
    /// nothing re-resolves a table's row after the fact the way a receipt
    /// re-derives a menu item's name would.
    /// </summary>
    public Result EnsureCanDelete()
    {
        if (State != TableState.Free)
        {
            return Result.Failure(Error.Conflict("floor.table_not_free", $"Table {Label} is not free."));
        }

        return Result.Success();
    }
}
