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

    private Order(Guid tableId, string tableLabel, int coverCount, DateTimeOffset openedAtUtc, bool isTakeaway)
    {
        TableId = tableId;
        TableLabel = tableLabel;
        CoverCount = coverCount;
        OpenedAtUtc = openedAtUtc;
        Status = OrderStatus.Open;
        IsTakeaway = isTakeaway;
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

        return new Order(tableId, tableLabel, coverCount, openedAtUtc, isTakeaway: false);
    }

    /// <summary>
    /// Opens a takeaway / counter-sale order (ORD-20) — rung up directly,
    /// with no Floor table involved at all. <see cref="TableId"/> stays
    /// <see cref="Guid.Empty"/>, but that is never the signal callers should
    /// check: <see cref="IsTakeaway"/> is. <c>Guid.Empty</c> is not a magic
    /// value anywhere else in this codebase and must not become one here.
    /// </summary>
    /// <param name="label">A ticket label for the order, e.g. a customer name or "Levantamento 7".</param>
    /// <param name="openedAtUtc">The current instant, from <see cref="Shared.Time.IClock"/>.</param>
    public static Order OpenTakeaway(string label, DateTimeOffset openedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("Label must not be empty.", nameof(label));
        }

        // CoverCount has no real meaning for a counter sale — fixed at 1
        // rather than adding a nullable column for a value nothing reads.
        return new Order(Guid.Empty, label, coverCount: 1, openedAtUtc, isTakeaway: true);
    }

    /// <summary>The Floor module table this order was opened against. <see cref="Guid.Empty"/> when <see cref="IsTakeaway"/>.</summary>
    public Guid TableId { get; private set; }

    /// <summary>The table's label at the moment it was opened, or the takeaway ticket label. See remarks.</summary>
    public string TableLabel { get; private set; }

    /// <summary>Number of guests seated. Always 1 for a takeaway order (ORD-20) — not a meaningful count there.</summary>
    public int CoverCount { get; private set; }

    /// <summary>True for a counter-sale order not tied to any Floor table (ORD-20). See <see cref="OpenTakeaway"/>.</summary>
    public bool IsTakeaway { get; private set; }

    /// <summary>Current lifecycle state.</summary>
    public OrderStatus Status { get; private set; }

    /// <summary>When the table was opened, in UTC.</summary>
    public DateTimeOffset OpenedAtUtc { get; private set; }

    /// <summary>When the order was closed, in UTC. Null while open.</summary>
    public DateTimeOffset? ClosedAtUtc { get; private set; }

    /// <summary>The lines rung up so far.</summary>
    public IReadOnlyList<OrderLine> Lines => _lines;

    /// <summary>
    /// A discount applied to the whole order (ORD-11), on top of any per-line
    /// discounts — or null when none is set. See <see cref="OrderLine.DiscountKind"/>
    /// for why this isn't named <c>DiscountType</c>.
    /// </summary>
    public DiscountType? DiscountKind { get; private set; }

    /// <summary>
    /// The order-level discount's magnitude — a percentage (0–100) or a euro
    /// amount, per <see cref="DiscountKind"/>. Null exactly when <see cref="DiscountKind"/> is.
    /// </summary>
    public decimal? DiscountValue { get; private set; }

    /// <summary>Sum of every line's own (already line-discounted) total. What the order would total with no order-level discount.</summary>
    public Money LinesSubtotal => _lines.Count == 0 ? Money.Zero : Money.Sum(_lines.Select(l => l.LineTotal));

    /// <summary>
    /// The amount <see cref="DiscountKind"/>/<see cref="DiscountValue"/> take off
    /// <see cref="LinesSubtotal"/>. Zero when no order-level discount is set.
    /// Clamped the same way as <see cref="OrderLine.DiscountAmount"/>.
    /// </summary>
    public Money OrderDiscountAmount
    {
        get
        {
            if (DiscountKind is null || DiscountValue is null)
            {
                return Money.Zero;
            }

            var subtotal = LinesSubtotal;
            var raw = DiscountKind == DiscountType.Percentage
                ? subtotal * (DiscountValue.Value / 100m)
                : Money.FromDecimal(DiscountValue.Value, subtotal.Currency);

            return raw > subtotal ? subtotal : raw;
        }
    }

    /// <summary>
    /// <see cref="LinesSubtotal"/> less <see cref="OrderDiscountAmount"/>. Zero
    /// for an order with no lines yet.
    /// </summary>
    public Money Total => LinesSubtotal - OrderDiscountAmount;

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
    /// Computes a bill split proportional to how many covers are in each
    /// group (ORD-17) — a table of 5 splitting 2-and-3, not necessarily
    /// evenly. <paramref name="covers"/> must sum to exactly
    /// <see cref="CoverCount"/>: it describes how this order's own covers
    /// are grouped, not an arbitrary weighting. Uses
    /// <see cref="Money.Allocate(ReadOnlySpan{int})"/> directly — the exact
    /// "split unevenly by covers" case that overload's own remarks call out.
    /// </summary>
    public Result<Money[]> SplitByCover(IReadOnlyList<int> covers)
    {
        if (covers.Count == 0)
        {
            return Result.Failure<Money[]>(
                Error.Validation("order.invalid_split", "At least one cover group is required."));
        }

        if (covers.Any(c => c < 1))
        {
            return Result.Failure<Money[]>(
                Error.Validation("order.invalid_split", "Each group must have at least 1 cover."));
        }

        var totalCovers = covers.Sum();
        if (totalCovers != CoverCount)
        {
            return Result.Failure<Money[]>(Error.Validation(
                "order.invalid_split",
                $"Cover groups sum to {totalCovers}, but this order has {CoverCount} cover(s)."));
        }

        return Result.Success(Total.Allocate([.. covers]));
    }

    /// <summary>
    /// Computes a bill split where each guest pays for specific items,
    /// rather than an equal share (ORD-16) — e.g. "Ana had the fish, Rui had
    /// the steak." <paramref name="groups"/> is one entry per guest's share;
    /// every line's <see cref="OrderLine.Quantity"/> must be allocated
    /// across the groups exactly once, with none left over and none
    /// double-counted. Returns one portion per line allocation, grouped the
    /// same shape as <paramref name="groups"/>, so a caller can show both
    /// the per-line breakdown and each group's total (their sum) without
    /// recomputing anything itself — the two can never disagree.
    /// </summary>
    /// <remarks>
    /// Each line's own <see cref="OrderLine.LineTotal"/> — already net of any
    /// line-level discount (ORD-11) and already zero if the line is voided
    /// (ORD-10) — is split across its <see cref="OrderLine.Quantity"/> units
    /// via <see cref="Money.Allocate(int)"/> before being handed out to
    /// whichever groups claim those units, so a discounted or voided line
    /// splits correctly regardless of how its quantity is divided up. Unlike
    /// <see cref="SplitEvenly"/> and <see cref="SplitByCover"/>, an
    /// order-level discount is <em>not</em> reflected here — prorating it
    /// first across lines and then across however each line's quantity is
    /// split between groups is a real design question (how do you fairly
    /// divide "10% off the table" between two guests who ordered different
    /// things?) that hasn't been settled, so this stays a known, documented
    /// gap rather than a guessed-at answer.
    /// </remarks>
    public Result<IReadOnlyList<IReadOnlyList<Money>>> SplitByItem(IReadOnlyList<IReadOnlyList<LineAllocation>> groups)
    {
        if (groups.Count == 0)
        {
            return Result.Failure<IReadOnlyList<IReadOnlyList<Money>>>(
                Error.Validation("order.invalid_split", "At least one group is required."));
        }

        var remainingQuantity = _lines.ToDictionary(l => l.Id, l => l.Quantity);
        var consumedCount = _lines.ToDictionary(l => l.Id, l => 0);
        var perUnitSharesByLine = _lines.ToDictionary(l => l.Id, l => l.LineTotal.Allocate(l.Quantity));
        var groupPortions = new List<IReadOnlyList<Money>>(groups.Count);

        foreach (var group in groups)
        {
            if (group.Count == 0)
            {
                return Result.Failure<IReadOnlyList<IReadOnlyList<Money>>>(
                    Error.Validation("order.invalid_split", "Each group must include at least one line."));
            }

            var portions = new List<Money>(group.Count);
            foreach (var allocation in group)
            {
                if (!remainingQuantity.TryGetValue(allocation.LineId, out var remaining))
                {
                    return Result.Failure<IReadOnlyList<IReadOnlyList<Money>>>(Error.NotFound(
                        "order.line_not_found", $"Line {allocation.LineId} was not found on this order."));
                }

                if (allocation.Quantity < 1 || allocation.Quantity > remaining)
                {
                    return Result.Failure<IReadOnlyList<IReadOnlyList<Money>>>(Error.Validation(
                        "order.invalid_split",
                        $"Line {allocation.LineId} was allocated {allocation.Quantity}, " +
                        $"more than its {remaining} remaining unallocated quantity."));
                }

                var consumed = consumedCount[allocation.LineId];
                var portion = Money.Sum(perUnitSharesByLine[allocation.LineId].Skip(consumed).Take(allocation.Quantity));

                remainingQuantity[allocation.LineId] = remaining - allocation.Quantity;
                consumedCount[allocation.LineId] = consumed + allocation.Quantity;
                portions.Add(portion);
            }

            groupPortions.Add(portions);
        }

        if (remainingQuantity.Values.Any(q => q > 0))
        {
            return Result.Failure<IReadOnlyList<IReadOnlyList<Money>>>(Error.Validation(
                "order.invalid_split", "Every line's quantity must be fully allocated across the groups."));
        }

        return Result.Success<IReadOnlyList<IReadOnlyList<Money>>>(groupPortions);
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

        // A takeaway order transferred onto a real table has, by definition,
        // become a dine-in one — this is the one place IsTakeaway can turn
        // false again. Never the reverse: OpenTakeaway is the only path in.
        IsTakeaway = false;

        return Result.Success();
    }

    /// <summary>
    /// Sets or clears a line's free-text kitchen note (ORD-06) — added after
    /// the line was already rung up. Notes are staff/kitchen visibility only;
    /// they never appear on a fiscal document, and a pre-bill showing them is
    /// incidental, not a Fiscal-module concern.
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
    /// Voids a line (ORD-10) — cancels it after it was already rung up, e.g.
    /// a dish that came out wrong or was never served. The line is never
    /// deleted (see <see cref="OrderLine.Void"/>): a receipt-adjacent audit
    /// trail of what was ordered and then cancelled, and why, matters more
    /// than a clean list. No manager-authorisation gate exists yet — ships
    /// ahead of that trigger, the same shape as ORD-11's discounts; IDN-11 is
    /// the real gate once staff accounts and roles exist.
    /// </summary>
    /// <param name="lineId">The line to void.</param>
    /// <param name="reason">
    /// Why the line is being voided. Required and non-blank — a void with no
    /// reason is exactly the kind of gap shrinkage-detection (DIF-13) would
    /// need to close later, so it's rejected outright rather than accepted
    /// and left empty.
    /// </param>
    /// <param name="voidedAtUtc">
    /// The current instant, from <see cref="Shared.Time.IClock"/> — never
    /// <c>DateTimeOffset.UtcNow</c> directly.
    /// </param>
    public Result VoidLine(Guid lineId, string? reason, DateTimeOffset voidedAtUtc)
    {
        if (Status != OrderStatus.Open)
        {
            return Result.Failure(
                Error.Conflict("order.not_open", "Cannot void a line on an order that is not open."));
        }

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
        {
            return Result.Failure(Error.NotFound("order.line_not_found", $"Line {lineId} was not found on this order."));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.Validation("order.void_reason_required", "A reason is required to void a line."));
        }

        if (line.IsVoided)
        {
            return Result.Failure(Error.Conflict("order.line_already_voided", "This line has already been voided."));
        }

        line.Void(reason.Trim(), voidedAtUtc);
        return Result.Success();
    }

    /// <summary>
    /// Changes how many of a line's item were ordered (ORD-03) — a guest
    /// asks for one more, or the waiter over-rang by mistake before it ever
    /// reached the kitchen. Recomputes <see cref="OrderLine.GrossBeforeDiscount"/>
    /// and therefore <see cref="OrderLine.LineTotal"/> automatically; a
    /// percentage discount scales with the new quantity, and a fixed discount
    /// stays fixed but re-clamps if it would now exceed the smaller gross
    /// (<see cref="OrderLine.DiscountAmount"/> already does this). This is
    /// deliberately not how a wrong or unwanted order is undone — that stays
    /// <see cref="VoidLine"/>, which requires a reason and preserves what was
    /// actually rung up for audit; editing the quantity of a voided line
    /// would defeat that, so it is rejected.
    /// </summary>
    /// <param name="lineId">The line to change.</param>
    /// <param name="quantity">The new quantity. At least 1 — to remove a line entirely, void it instead.</param>
    public Result SetLineQuantity(Guid lineId, int quantity)
    {
        if (Status != OrderStatus.Open)
        {
            return Result.Failure(
                Error.Conflict("order.not_open", "Cannot change a line's quantity on an order that is not open."));
        }

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
        {
            return Result.Failure(Error.NotFound("order.line_not_found", $"Line {lineId} was not found on this order."));
        }

        if (quantity < 1)
        {
            return Result.Failure(Error.Validation("order.invalid_quantity", "Quantity must be at least 1."));
        }

        if (line.IsVoided)
        {
            return Result.Failure(
                Error.Conflict("order.line_voided", "Cannot change the quantity of a voided line."));
        }

        line.SetQuantity(quantity);
        return Result.Success();
    }

    /// <summary>
    /// Sets or clears a discount on one line (ORD-11) — a manager comping half
    /// off a dish that arrived late, say. No manager-authorisation gate exists
    /// yet: this ships ahead of that trigger the same way CAT-13's 86-ing and
    /// CAT-19's repricing did, with IDN-11 tracked separately as the real gate
    /// once staff accounts and roles exist.
    /// </summary>
    /// <param name="lineId">The line to discount.</param>
    /// <param name="kind">
    /// The discount's shape, or null together with <paramref name="value"/> to
    /// clear an existing one. Providing exactly one of the pair is rejected.
    /// </param>
    /// <param name="value">
    /// A percentage (0–100] or a euro amount, per <paramref name="kind"/>. A
    /// fixed amount that would exceed the line's own pre-discount total is
    /// rejected rather than silently clamped, so a mistyped value surfaces
    /// immediately instead of quietly comping the whole line.
    /// </param>
    public Result SetLineDiscount(Guid lineId, DiscountType? kind, decimal? value)
    {
        if (Status != OrderStatus.Open)
        {
            return Result.Failure(
                Error.Conflict("order.not_open", "Cannot change a line's discount on an order that is not open."));
        }

        var line = _lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
        {
            return Result.Failure(Error.NotFound("order.line_not_found", $"Line {lineId} was not found on this order."));
        }

        var validation = ValidateDiscount(kind, value, line.GrossBeforeDiscount);
        if (validation.IsFailure)
        {
            return validation;
        }

        line.SetDiscount(kind, value);
        return Result.Success();
    }

    /// <summary>
    /// Sets or clears a discount on the whole order (ORD-11), applied on top
    /// of any per-line discounts already set — a regular's 10% loyalty
    /// discount on the whole table, say. Same "no authorisation gate yet"
    /// shape as <see cref="SetLineDiscount"/>.
    /// </summary>
    /// <param name="kind">
    /// The discount's shape, or null together with <paramref name="value"/> to
    /// clear an existing one.
    /// </param>
    /// <param name="value">
    /// A percentage (0–100] or a euro amount, per <paramref name="kind"/>. A
    /// fixed amount that would exceed the current <see cref="LinesSubtotal"/>
    /// is rejected — which also means an order with no lines yet can't take a
    /// fixed order-level discount, the same as it can't generate a pre-bill.
    /// </param>
    public Result SetDiscount(DiscountType? kind, decimal? value)
    {
        if (Status != OrderStatus.Open)
        {
            return Result.Failure(
                Error.Conflict("order.not_open", "Cannot change an order's discount on an order that is not open."));
        }

        var validation = ValidateDiscount(kind, value, LinesSubtotal);
        if (validation.IsFailure)
        {
            return validation;
        }

        DiscountKind = kind;
        DiscountValue = value;
        return Result.Success();
    }

    /// <summary>
    /// Shared validation for <see cref="SetLineDiscount"/> and <see cref="SetDiscount"/>:
    /// both a type and a value, or neither; a percentage in (0, 100]; a fixed
    /// amount that is positive and doesn't exceed <paramref name="cap"/> — the
    /// line's own gross for a line discount, <see cref="LinesSubtotal"/> for an
    /// order-level one.
    /// </summary>
    private static Result ValidateDiscount(DiscountType? kind, decimal? value, Money cap)
    {
        if (kind is null != value is null)
        {
            return Result.Failure(Error.Validation(
                "order.invalid_discount", "A discount needs both a type and a value, or neither to clear it."));
        }

        // kind and value are null together (checked above), so kind being
        // non-null here guarantees value is too — this pattern match makes
        // that guarantee visible to nullable flow analysis, rather than a
        // null-forgiving `value.Value` the compiler can't actually verify.
        if (kind is null || value is not decimal discountValue)
        {
            return Result.Success();
        }

        if (kind == DiscountType.Percentage)
        {
            return discountValue is > 0 and <= 100
                ? Result.Success()
                : Result.Failure(Error.Validation(
                    "order.invalid_discount", "A percentage discount must be greater than 0 and at most 100."));
        }

        if (discountValue <= 0)
        {
            return Result.Failure(Error.Validation("order.invalid_discount", "A fixed discount must be greater than 0."));
        }

        var amount = Money.FromDecimal(discountValue, cap.Currency);
        return amount > cap
            ? Result.Failure(Error.Validation(
                "order.invalid_discount", "A fixed discount cannot exceed the total it's applied to."))
            : Result.Success();
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

    /// <summary>
    /// Closes the order. Requires at least one line and that it is not
    /// already closed. This check counts every line, voided (ORD-10) or not
    /// — a fully-voided order still passes it and transitions to
    /// <see cref="OrderStatus.Closed"/> here. It doesn't actually get to
    /// stay closed, though: <c>BuildFiscalLines</c> (<c>OrderEndpoints</c>)
    /// omits voided lines entirely, so a fully-voided order produces an
    /// empty fiscal-line list, which <c>IFiscalProvider.IssueSimplifiedInvoiceAsync</c>
    /// already rejects with <c>fiscal.no_lines</c> — a pre-existing guard,
    /// not new logic invented for this case. <c>CloseOrderAsync</c> only
    /// persists this transition after the fiscal document is actually
    /// issued, so that rejection leaves the order genuinely still `Open` in
    /// the database, not closed-with-nothing-to-show-for-it. Deliberately
    /// not guarded here too, rather than guessing at a fiscal-correctness
    /// rule (can AT accept a €0.00 invoice?) nobody has confirmed — see
    /// <c>docs/fiscal/README.md</c>.
    /// </summary>
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
