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
        IReadOnlyList<SelectedModifier> modifiers,
        string? course)
    {
        OrderId = orderId;
        MenuItemId = menuItemId;
        ItemName = itemName;
        UnitPrice = unitPrice;
        VatRateFraction = vatRateFraction;
        Quantity = quantity;
        Course = course;

        foreach (var modifier in modifiers)
        {
            _modifiers.Add(new OrderLineModifier(Id, modifier.ModifierId, modifier.Name, modifier.PriceDelta));
        }
    }

    /// <summary>The order this line belongs to.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>
    /// Reassigns this line to a different order — used only when transferring
    /// it between tables mid-service (ORD-13). Only <see cref="Order.ReceiveLine"/>
    /// calls this.
    /// </summary>
    internal void ReassignToOrder(Guid newOrderId)
    {
        OrderId = newOrderId;
    }

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

    /// <summary>
    /// Which course the item was served at, at the time of sale (ORD-07) —
    /// a plain string copied from the catalog item's own <c>Course</c> (CAT-14)
    /// when the line was rung up, the same snapshot convention as
    /// <see cref="ItemName"/>. Null when the item had no course assigned.
    /// Ordering never references Catalog's <c>Course</c> enum directly (module
    /// boundaries) — the API layer resolves and validates it before calling
    /// <see cref="Order.AddLine"/>.
    /// </summary>
    public string? Course { get; private set; }

    /// <summary>Changes <see cref="Quantity"/> (ORD-03). Only <see cref="Order.SetLineQuantity"/> calls this.</summary>
    internal void SetQuantity(int quantity)
    {
        Quantity = quantity;
    }

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
    /// A discount applied to this one line (ORD-11), or null when none is set.
    /// Named <c>DiscountKind</c>, not <c>DiscountType</c>, so it doesn't shadow
    /// the <see cref="Domain.DiscountType"/> enum's own name inside this class —
    /// C# resolves a bare type name to an instance member first when both share
    /// an identifier, which would make the enum's own members unreachable here.
    /// </summary>
    public DiscountType? DiscountKind { get; private set; }

    /// <summary>
    /// The discount's magnitude — a percentage (0–100) when <see cref="DiscountKind"/>
    /// is <see cref="Domain.DiscountType.Percentage"/>, or a euro amount when it's
    /// <see cref="Domain.DiscountType.FixedAmount"/>. Null exactly when <see cref="DiscountKind"/> is.
    /// </summary>
    public decimal? DiscountValue { get; private set; }

    /// <summary>
    /// (Unit price + modifiers) times quantity, before <see cref="DiscountAmount"/>
    /// is taken off. What this line would total with no discount applied.
    /// </summary>
    public Money GrossBeforeDiscount => (UnitPrice + ModifiersTotal) * Quantity;

    /// <summary>
    /// The amount <see cref="DiscountKind"/>/<see cref="DiscountValue"/> take off
    /// <see cref="GrossBeforeDiscount"/>. Zero when no discount is set. Clamped so
    /// it can never exceed the line's own gross — defence in depth on top of the
    /// same check <see cref="Order.SetLineDiscount"/> already performs before a
    /// fixed discount is accepted.
    /// </summary>
    public Money DiscountAmount
    {
        get
        {
            if (DiscountKind is null || DiscountValue is null)
            {
                return Money.ZeroIn(UnitPrice.Currency);
            }

            var gross = GrossBeforeDiscount;
            var raw = DiscountKind == DiscountType.Percentage
                ? gross * (DiscountValue.Value / 100m)
                : Money.FromDecimal(DiscountValue.Value, gross.Currency);

            return raw > gross ? gross : raw;
        }
    }

    /// <summary>
    /// <see cref="GrossBeforeDiscount"/> less <see cref="DiscountAmount"/>, or
    /// zero outright when <see cref="IsVoided"/> — a voided line contributes
    /// nothing to what the guest owes, discount or not. Not persisted —
    /// always derived, the same as <see cref="Order.Total"/>.
    /// </summary>
    public Money LineTotal => IsVoided ? Money.ZeroIn(UnitPrice.Currency) : GrossBeforeDiscount - DiscountAmount;

    /// <summary>Sets or clears this line's discount. Only <see cref="Order.SetLineDiscount"/> calls this.</summary>
    internal void SetDiscount(DiscountType? kind, decimal? value)
    {
        DiscountKind = kind;
        DiscountValue = value;
    }

    /// <summary>
    /// True once this line has been voided (ORD-10) — cancelled after being
    /// rung up, e.g. a dish that came out wrong. The line is never deleted:
    /// <see cref="ItemName"/>/<see cref="UnitPrice"/>/<see cref="Quantity"/>
    /// stay exactly as they were, so what was ordered and then cancelled
    /// remains visible for audit — only <see cref="LineTotal"/> drops to zero.
    /// </summary>
    public bool IsVoided { get; private set; }

    /// <summary>Why the line was voided. Required when voiding — never null while <see cref="IsVoided"/> is true.</summary>
    public string? VoidReason { get; private set; }

    /// <summary>When the line was voided, in UTC. Null while not voided.</summary>
    public DateTimeOffset? VoidedAtUtc { get; private set; }

    /// <summary>Voids this line. Only <see cref="Order.VoidLine"/> calls this.</summary>
    internal void Void(string reason, DateTimeOffset voidedAtUtc)
    {
        IsVoided = true;
        VoidReason = reason;
        VoidedAtUtc = voidedAtUtc;
    }

    /// <summary>
    /// True once this line has been sent to the kitchen (ORD-07/08). No real
    /// kitchen exists to route to yet (KIT-01…09/AGT are unbuilt) — this is
    /// the seam a future ticket-printing consumer will read from, the same
    /// "mechanism before the trigger" shape CAT-13/14/15/19 already used.
    /// </summary>
    public bool IsFired { get; private set; }

    /// <summary>When the line was fired, in UTC. Null while not yet fired.</summary>
    public DateTimeOffset? FiredAtUtc { get; private set; }

    /// <summary>Marks this line fired. Only <see cref="Order.FireLines"/> calls this.</summary>
    internal void Fire(DateTimeOffset firedAtUtc)
    {
        IsFired = true;
        FiredAtUtc = firedAtUtc;
    }
}
