using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;

namespace Brasa.Modules.Ordering.Domain;

/// <summary>
/// One line of an <see cref="Order"/> — a quantity of a menu item at the price
/// and VAT rate in effect when it was ordered.
/// </summary>
/// <remarks>
/// <see cref="ItemName"/>, <see cref="UnitPrice"/> and <see cref="VatRateFraction"/>
/// are copied from the catalog at creation time and never change afterwards, even
/// if the menu item's price changes later. That is correctness, not
/// denormalisation: a receipt must show what the item cost when it was sold. See
/// <c>docs/architecture/module-boundaries.md</c>.
/// </remarks>
public sealed class OrderLine : Entity
{
    private readonly List<OrderLineModifier> _modifiers = [];

    private OrderLine()
    {
        // EF Core materialisation.
        ItemName = string.Empty;
    }

    /// <summary>
    /// Creates a line. Only <see cref="Order.AddLine"/> constructs one, so every
    /// line is guaranteed to belong to an order that was open when it was added.
    /// </summary>
    internal OrderLine(
        Guid orderId,
        Guid menuItemId,
        string itemName,
        Money unitPrice,
        decimal vatRateFraction,
        int quantity,
        IReadOnlyList<SelectedModifier> modifiers)
    {
        OrderId = orderId;
        MenuItemId = menuItemId;
        ItemName = itemName;
        UnitPrice = unitPrice;
        VatRateFraction = vatRateFraction;
        Quantity = quantity;

        foreach (var modifier in modifiers)
        {
            _modifiers.Add(new OrderLineModifier(Id, modifier.ModifierId, modifier.Name, modifier.PriceDelta));
        }
    }

    /// <summary>The order this line belongs to.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>
    /// The catalog item this line was rung up from. A value reference only — the
    /// Ordering module never joins across the schema boundary into Catalog's
    /// tables.
    /// </summary>
    public Guid MenuItemId { get; private set; }

    /// <summary>Item name at the time of sale.</summary>
    public string ItemName { get; private set; }

    /// <summary>
    /// Unit price at the time of sale, <b>VAT-inclusive</b> — snapshotted from
    /// the catalog item's selling price. This is what the guest pays; the
    /// fiscal document derives net and VAT from it, not the reverse.
    /// </summary>
    public Money UnitPrice { get; private set; }

    /// <summary>VAT rate at the time of sale, as a fraction (0.13 for 13%).</summary>
    public decimal VatRateFraction { get; private set; }

    /// <summary>How many were ordered.</summary>
    public int Quantity { get; private set; }

    /// <summary>Modifiers selected on this line, snapshotted at the time of sale. CAT-03/CAT-04.</summary>
    public IReadOnlyList<OrderLineModifier> Modifiers => _modifiers;

    /// <summary>Free-text kitchen note, e.g. "sem cebola" (ORD-06). Null when none was added.</summary>
    public string? Notes { get; private set; }

    /// <summary>Sets or clears this line's kitchen note. Only <see cref="Order.SetLineNotes"/> calls this.</summary>
    internal void SetNotes(string? notes)
    {
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    /// <summary>
    /// Sum of every selected modifier's price delta, per unit — not yet
    /// multiplied by <see cref="Quantity"/>. Zero when no modifiers were
    /// selected.
    /// </summary>
    public Money ModifiersTotal => _modifiers.Count == 0
        ? Money.ZeroIn(UnitPrice.Currency)
        : Money.Sum(_modifiers.Select(m => m.PriceDelta));

    /// <summary>
    /// (Unit price + modifiers) times quantity. Not persisted — always
    /// derived, the same as <see cref="Order.Total"/>.
    /// </summary>
    public Money LineTotal => (UnitPrice + ModifiersTotal) * Quantity;
}
