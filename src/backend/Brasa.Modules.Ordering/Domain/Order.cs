using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;

namespace Brasa.Modules.Ordering.Domain;

/// <summary>
/// A table's running order — the aggregate root for taking, pricing and closing
/// a sale.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TableLabel"/> is free text in I0 because the floor-plan module
/// (epic FLR) does not exist yet. It becomes a real <c>TableId</c> reference in
/// I1 without changing anything about how a line is priced or a bill is split.
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

    private Order(string tableLabel, int coverCount, DateTimeOffset openedAtUtc)
    {
        TableLabel = tableLabel;
        CoverCount = coverCount;
        OpenedAtUtc = openedAtUtc;
        Status = OrderStatus.Open;
    }

    /// <summary>Opens a new order for a table.</summary>
    /// <param name="tableLabel">Free-text table identifier, e.g. "Mesa 5".</param>
    /// <param name="coverCount">Number of guests. At least 1.</param>
    /// <param name="openedAtUtc">
    /// The current instant, from <see cref="Shared.Time.IClock"/> — never
    /// <c>DateTimeOffset.UtcNow</c> directly.
    /// </param>
    public static Order Open(string tableLabel, int coverCount, DateTimeOffset openedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(tableLabel))
        {
            throw new ArgumentException("Table label must not be empty.", nameof(tableLabel));
        }

        if (coverCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(coverCount), coverCount, "Cover count must be at least 1.");
        }

        return new Order(tableLabel, coverCount, openedAtUtc);
    }

    /// <summary>Free-text table identifier. See remarks — becomes a real reference in I1.</summary>
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
    public Result<OrderLine> AddLine(Guid menuItemId, string itemName, Money unitPrice, decimal vatRateFraction, int quantity)
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

        var line = new OrderLine(Id, menuItemId, itemName, unitPrice, vatRateFraction, quantity);
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
