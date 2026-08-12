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

    /// <summary>
    /// Which seating group this table belongs to, if any (FLR-05) — a
    /// large party seated across 2+ physical tables pushed together, e.g.
    /// "Mesa 5" and "Mesa 6" sharing one 8-seat footprint. A plain shared
    /// value, not a separate <c>TableGroup</c> entity: every field a group
    /// needs (which tables, combined seats) is computed from the member
    /// tables themselves, so there is nothing else to own.
    /// </summary>
    /// <remarks>
    /// Deliberately does not let a grouped table's siblings occupy
    /// together in one action yet — opening an order still targets one
    /// specific table id, the same call as always. What grouping *does*
    /// change is real, not cosmetic: <see cref="Occupy"/> refuses any
    /// table with a non-null <see cref="GroupId"/> outright, so once
    /// grouped, neither table can be individually seated — a host can't
    /// accidentally put a different party at "Mesa 6" while it is pushed
    /// together with "Mesa 5" for a large one. Un-grouping
    /// (<see cref="LeaveGroup"/>) is required before either table can be
    /// occupied again, individually or as part of a new group. Building
    /// "open one order that seats the whole group at once" is a separate,
    /// deferred follow-up — it would touch several already-shipped,
    /// already-tested endpoints (<c>OpenOrderAsync</c>, <c>ClearTableAsync</c>,
    /// <c>TransferOrderAsync</c>) and deserves its own careful pass rather
    /// than riding in on this one.
    /// </remarks>
    public Guid? GroupId { get; private set; }

    /// <summary>
    /// Seats guests and opens the table for ordering. Requires the table
    /// to be free and not part of a seating group (FLR-05) — see
    /// <see cref="GroupId"/>'s own remarks for why grouped tables refuse
    /// individual seating rather than silently allowing it.
    /// </summary>
    public Result Occupy()
    {
        if (State != TableState.Free)
        {
            return Result.Failure(Error.Conflict("floor.table_not_free", $"Table {Label} is not free."));
        }

        if (GroupId is not null)
        {
            return Result.Failure(Error.Conflict(
                "floor.table_grouped", $"Table {Label} is part of a seating group — ungroup it first."));
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

        if (GroupId is not null)
        {
            return Result.Failure(Error.Conflict(
                "floor.table_grouped", $"Table {Label} is part of a seating group — ungroup it first."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Joins this table to a seating group (FLR-05) so 2+ physical tables
    /// can be pushed together for a large party. Requires
    /// <see cref="TableState.Free"/> and no existing group — merging
    /// starts from a clean state, not mid-service.
    /// </summary>
    public Result JoinGroup(Guid groupId)
    {
        if (State != TableState.Free)
        {
            return Result.Failure(Error.Conflict("floor.table_not_free", $"Table {Label} is not free."));
        }

        if (GroupId is not null)
        {
            return Result.Failure(Error.Conflict(
                "floor.table_already_grouped", $"Table {Label} is already part of a seating group."));
        }

        GroupId = groupId;
        return Result.Success();
    }

    /// <summary>
    /// Leaves its seating group (FLR-05), freeing this table to be
    /// occupied individually or grouped again. In practice a grouped
    /// table is always <see cref="TableState.Free"/> already —
    /// <see cref="Occupy"/> refuses one otherwise — but the guard is kept
    /// explicit rather than assumed.
    /// </summary>
    public Result LeaveGroup()
    {
        if (State != TableState.Free)
        {
            return Result.Failure(Error.Conflict("floor.table_not_free", $"Table {Label} is not free."));
        }

        GroupId = null;
        return Result.Success();
    }
}
