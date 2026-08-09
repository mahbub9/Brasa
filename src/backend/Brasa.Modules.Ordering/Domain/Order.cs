using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;

namespace Brasa.Modules.Ordering.Domain;

/// <summary>
/// A table's running order — the aggregate root for taking, pricing and closing
/// a sale.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TableId"/> is a plain, opaque reference to a
/// <c>Brasa.Modules.Floor.Domain.Table</c> — the same pattern
/// <see cref="OrderLine.MenuItemId"/> uses for a Catalog item. Ordering never
/// queries the Floor module directly; the API layer resolves the table,
/// transitions its state, and passes its label down here to snapshot. See
/// <c>docs/architecture/module-boundaries.md</c>.
/// </para>
/// <para>
/// <see cref="TableLabel"/> is still stored as its own column, snapshotted at
/// open time exactly like an order line's item name — if a table is ever
/// relabelled, a past order keeps showing what it was called when it was
/// opened.
/// </para>
/// <para>
/// Basic input validation (empty strings, non-positive counts) throws, matching
/// the convention already used by <c>Money</c> and the Catalog entities.
/// Business-rule violations that a waiter can trigger by tapping the wrong
/// button — adding a line to a closed order, closing an empty one — return
/// <see cref="Result"/> instead. See <c>docs/architecture/conventions.md</c>.
/// </para>
/// </remarks>
public sealed class Order : Entity
{
    private readonly List<OrderLine> _lines = [];

    private Order()
    {
        // EF Core materialisation.
        TableLabel = string.Empty;
    }

    private Order(Guid tableId, string tableLabel, int coverCount, DateTimeOffset openedAtUtc)
    {
        TableId = tableId;
        TableLabel = tableLabel;
        CoverCount = coverCount;
        OpenedAtUtc = openedAtUtc;
        Status = OrderStatus.Open;
    }

    /// <summary>Opens a new order for a table.</summary>
    /// <param name="tableId">The Floor module table's id, already transitioned to occupied by the caller.</param>
    /// <param name="tableLabel">The table's label at the moment it was opened, e.g. "Mesa 5".</param>
    /// <param name="coverCount">Number of guests. At least 1.</param>
    /// <param name="openedAtUtc">
    /// The current instant, from <see cref="Shared.Time.IClock"/> — never
    /// <c>DateTimeOffset.UtcNow</c> directly.
    /// </param>
    public static Order Open(Guid tableId, string tableLabel, int coverCount, DateTimeOffset openedAtUtc)
    {
        if (tableId == Guid.Empty)
        {
            throw new ArgumentException("Table id must not be empty.", nameof(tableId));
        }

        if (string.IsNullOrWhiteSpace(tableLabel))
        {
            throw new ArgumentException("Table label must not be empty.", nameof(tableLabel));
        }

        if (coverCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(coverCount), coverCount, "Cover count must be at least 1.");
        }

        return new Order(tableId, tableLabel, coverCount, openedAtUtc);
    }

    /// <summary>The Floor module table this order was opened against.</summary>
    public Guid TableId { get; private set; }

    /// <summary>The table's label at the moment it was opened. See remarks.</summary>
    public string TableLabel { get; private set; }

    /// <summary>Number of guests seated.</summary>
    public int CoverCount { get; private set; }

    /// <summary>Current lifecycle state.</summary>
    public OrderStatus Status { get; private set; }

    /// <summary>When the table was opened, in UTC.</summary>
    public DateTimeOffset OpenedAtUtc { get; private set; }

    /// <summary>When the order was closed, in UTC. Null while open.</summary>
    public DateTimeOffset? ClosedAtUtc { get; private set; }

    /// <summary>The lines rung up so far.</summary>
    public IReadOnlyList<OrderLine> Lines => _lines;

    /// <summary>Sum of every line. Zero for an order with no lines yet.</summary>
    public Money Total => _lines.Count == 0 ? Money.Zero : Money.Sum(_lines.Select(l => l.LineTotal));

    /// <summary>
    /// Rings up a quantity of a menu item, copying its current price and VAT
    /// rate onto the line.
    /// </summary>
    /// <param name="menuItemId">The catalog item's id.</param>
    /// <param name="itemName">Item name to snapshot onto the line.</param>
    /// <param name="unitPrice">Unit price to snapshot onto the line.</param>
    /// <param name="vatRateFraction">VAT rate to snapshot onto the line, e.g. 0.13m.</param>
    /// <param name="quantity">How many. At least 1.</param>
    /// <param name="modifiers">
    /// Modifiers selected for this line, already resolved and validated
    /// against the catalog by the caller (Ordering never resolves a modifier
    /// id itself — see <see cref="SelectedModifier"/>). Empty when the item
    /// has no modifier groups.
    /// </param>
    public Result<OrderLine> AddLine(
        Guid menuItemId,
        string itemName,
        Money unitPrice,
        decimal vatRateFraction,
        int quantity,
        IReadOnlyList<SelectedModifier>? modifiers = null)
    {
        if (Status != OrderStatus.Open)
        {
            return Result.Failure<OrderLine>(
                Error.Conflict("order.not_open", "Cannot add a line to an order that is not open."));
        }

        if (quantity < 1)
        {
            return Result.Failure<OrderLine>(
                Error.Validation("order.invalid_quantity", "Quantity must be at least 1."));
        }

        var line = new OrderLine(Id, menuItemId, itemName, unitPrice, vatRateFraction, quantity, modifiers ?? []);
        _lines.Add(line);
        return Result.Success(line);
    }

    /// <summary>
    /// Computes an even split of the current total into <paramref name="parts"/>
    /// shares, without changing order state. See <see cref="Money.Allocate(int)"/>
    /// for why this is never plain division.
    /// </summary>
    public Result<Money[]> SplitEvenly(int parts)
    {
        if (parts < 1)
        {
            return Result.Failure<Money[]>(
                Error.Validation("order.invalid_split", "Split count must be at least 1."));
        }

        return Result.Success(Total.Allocate(parts));
    }

    /// <summary>
    /// Computes a bill split where each guest pays for specific items,
    /// rather than an equal share (ORD-16) — e.g. "Ana had the fish, Rui had
    /// the steak." <paramref name="groups"/> is one entry per guest's share;
    /// every line's <see cref="OrderLine.Quantity"/> must be allocated
    /// across the groups exactly once, with none left over and none
    /// double-counted. Unlike <see cref="SplitEvenly"/>, this never needs
    /// <see cref="Money.Allocate(int)"/>'s remainder distribution — each
    /// allocation's portion is an exact multiple of the line's own per-unit
    /// price, so the shares are exact by construction.
    /// </summary>
    public Result<IReadOnlyList<Money>> SplitByItem(IReadOnlyList<IReadOnlyList<LineAllocation>> groups)
    {
        if (groups.Count == 0)
        {
            return Result.Failure<IReadOnlyList<Money>>(
                Error.Validation("order.invalid_split", "At least one group is required."));
        }

        var remainingQuantity = _lines.ToDictionary(l => l.Id, l => l.Quantity);
        var totals = new List<Money>(groups.Count);

        foreach (var group in groups)
        {
            if (group.Count == 0)
            {
                return Result.Failure<IReadOnlyList<Money>>(
                    Error.Validation("order.invalid_split", "Each group must include at least one line."));
            }

            var portions = new List<Money>(group.Count);
            foreach (var allocation in group)
            {
                if (!remainingQuantity.TryGetValue(allocation.LineId, out var remaining))
                {
                    return Result.Failure<IReadOnlyList<Money>>(Error.NotFound(
                        "order.line_not_found", $"Line {allocation.LineId} was not found on this order."));
                }

                if (allocation.Quantity < 1 || allocation.Quantity > remaining)
                {
                    return Result.Failure<IReadOnlyList<Money>>(Error.Validation(
                        "order.invalid_split",
                        $"Line {allocation.LineId} was allocated {allocation.Quantity}, " +
                        $"more than its {remaining} remaining unallocated quantity."));
                }

                var line = _lines.First(l => l.Id == allocation.LineId);
                remainingQuantity[allocation.LineId] = remaining - allocation.Quantity;
                portions.Add((line.UnitPrice + line.ModifiersTotal) * allocation.Quantity);
            }

            totals.Add(Money.Sum(portions));
        }

        if (remainingQuantity.Values.Any(q => q > 0))
        {
            return Result.Failure<IReadOnlyList<Money>>(Error.Validation(
                "order.invalid_split", "Every line's quantity must be fully allocated across the groups."));
        }

        return Result.Success<IReadOnlyList<Money>>(totals);
    }

    /// <summary>
    /// Removes a line from this order so it can be attached to a different
    /// one (ORD-13) — a dish moving to the table a guest is joining, or a
    /// large party splitting across two tables mid-service. The line itself
    /// carries over untouched: price, VAT rate, modifiers and notes were all
    /// already snapshotted at the time it was rung up. Requires this order to
    /// be <c>Open</c>.
    /// </summary>
    public Result<OrderLine> DetachLine(Guid lineId)
    {
        if (Status != OrderStatus.Open)
        {
            return Result.Failure<OrderLine>(
                Error.Conflict("order.not_open", "Cannot transfer a line from an order that is not open."));
        }

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
        {
            return Result.Failure<OrderLine>(
                Error.NotFound("order.line_not_found", $"Line {lineId} was not found on this order."));
        }

        _lines.Remove(line);
        return Result.Success(line);
    }

    /// <summary>
    /// Attaches a line detached from a different order (ORD-13). Requires
    /// this order to be <c>Open</c> — call sites should check both orders'
    /// status before calling <see cref="DetachLine"/> at all, so this failing
    /// after a successful detach should not happen in practice; it is still
    /// checked here rather than trusted, the same defence-in-depth as
    /// <see cref="EnsureCanGeneratePreBill"/>.
    /// </summary>
    public Result ReceiveLine(OrderLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (Status != OrderStatus.Open)
        {
            return Result.Failure(
                Error.Conflict("order.not_open", "Cannot transfer a line onto an order that is not open."));
        }

        line.ReassignToOrder(Id);
        _lines.Add(line);
        return Result.Success();
    }

    /// <summary>
    /// Marks this order merged into another one (ORD-14) — every line must
    /// already have moved out via <see cref="DetachLine"/> first, so nothing
    /// about this order is billed or fiscally issued; it simply stops being
    /// a live order. Distinct from <see cref="Close"/>: a merged order never
    /// gets a fiscal document, a closed one always does.
    /// </summary>
    public Result MarkMerged()
    {
        if (Status != OrderStatus.Open)
        {
            return Result.Failure(
                Error.Conflict("order.not_open", "Cannot mark as merged an order that is not open."));
        }

        if (_lines.Count != 0)
        {
            return Result.Failure(
                Error.Validation("order.not_empty", "Cannot mark an order merged while it still has lines."));
        }

        Status = OrderStatus.Merged;
        return Result.Success();
    }

    /// <summary>
    /// Moves this order onto a different table (ORD-12) — a party changing
    /// seats mid-service, not a new order. The caller (API layer) is
    /// responsible for freeing the old <c>Floor.Table</c> and occupying the
    /// new one; Ordering only ever holds the opaque id/label pair, the same
    /// pattern <see cref="Open"/> uses.
    /// </summary>
    /// <param name="newTableId">The Floor module table's id being transferred to.</param>
    /// <param name="newTableLabel">The new table's label at the moment of transfer, to snapshot.</param>
    public Result TransferToTable(Guid newTableId, string newTableLabel)
    {
        if (Status != OrderStatus.Open)
        {
            return Result.Failure(
                Error.Conflict("order.not_open", "Cannot transfer a table on an order that is not open."));
        }

        if (newTableId == Guid.Empty)
        {
            throw new ArgumentException("Table id must not be empty.", nameof(newTableId));
        }

        if (string.IsNullOrWhiteSpace(newTableLabel))
        {
            throw new ArgumentException("Table label must not be empty.", nameof(newTableLabel));
        }

        TableId = newTableId;
        TableLabel = newTableLabel;
        return Result.Success();
    }

    /// <summary>
    /// Sets or clears a line's free-text kitchen note (ORD-06) — added after
    /// the line was already rung up, since editing a line itself isn't built
    /// yet (ORD-03: add only until I2). Notes are staff/kitchen visibility
    /// only; they never appear on a fiscal document, and a pre-bill showing
    /// them is incidental, not a Fiscal-module concern.
    /// </summary>
    /// <param name="lineId">The line to annotate.</param>
    /// <param name="notes">
    /// The note, or null/whitespace to clear it. Trimmed; rejected outright
    /// past 300 characters rather than silently truncated.
    /// </param>
    public Result SetLineNotes(Guid lineId, string? notes)
    {
        if (Status != OrderStatus.Open)
        {
            return Result.Failure(
                Error.Conflict("order.not_open", "Cannot change a line's notes on an order that is not open."));
        }

        if (notes is { Length: > 300 })
        {
            return Result.Failure(Error.Validation("order.notes_too_long", "Notes must be 300 characters or fewer."));
        }

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
        {
            return Result.Failure(Error.NotFound("order.line_not_found", $"Line {lineId} was not found on this order."));
        }

        line.SetNotes(notes);
        return Result.Success();
    }

    /// <summary>
    /// Guards generating a pre-bill preview for the table — a <em>documento não
    /// fiscal</em>, never an invoice (ORD-18). See
    /// <c>docs/fiscal/README.md</c>: issuing it as a fiscal document would
    /// fiscalise every table that merely asks to see the bill, so this only
    /// checks order state and never touches the Fiscal module.
    /// </summary>
    public Result EnsureCanGeneratePreBill()
    {
        if (Status != OrderStatus.Open)
        {
            return Result.Failure(
                Error.Conflict("order.not_open", "Cannot generate a pre-bill for an order that is not open."));
        }

        if (_lines.Count == 0)
        {
            return Result.Failure(
                Error.Validation("order.empty", "Cannot generate a pre-bill for an order with no lines."));
        }

        return Result.Success();
    }

    /// <summary>Closes the order. Requires at least one line and that it is not already closed.</summary>
    /// <param name="closedAtUtc">The current instant, from <see cref="Shared.Time.IClock"/>.</param>
    public Result Close(DateTimeOffset closedAtUtc)
    {
        if (Status == OrderStatus.Closed)
        {
            return Result.Failure(Error.Conflict("order.already_closed", "Order is already closed."));
        }

        if (_lines.Count == 0)
        {
            return Result.Failure(Error.Validation("order.empty", "Cannot close an order with no lines."));
        }

        Status = OrderStatus.Closed;
        ClosedAtUtc = closedAtUtc;
        return Result.Success();
    }
}
