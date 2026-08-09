using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;

namespace Brasa.Modules.Ordering.Domain;

/// <summary>
/// A modifier selected on an <see cref="OrderLine"/> at the time it was rung
/// up, e.g. "Extra queijo" (+€1.00). CAT-03/CAT-04.
/// </summary>
/// <remarks>
/// <see cref="Name"/> and <see cref="PriceDelta"/> are copied from the
/// catalog's <c>Modifier</c> at selection time, the same snapshot reasoning
/// as <see cref="OrderLine.ItemName"/> and <see cref="OrderLine.UnitPrice"/> —
/// a receipt must show what was actually charged, not what the modifier costs
/// today. <see cref="ModifierId"/> is an opaque reference only, the same
/// pattern as <see cref="OrderLine.MenuItemId"/>.
/// </remarks>
public sealed class OrderLineModifier : Entity
{
    private OrderLineModifier()
    {
        // EF Core materialisation.
        Name = string.Empty;
    }

    /// <summary>Creates a selected modifier. Only <see cref="OrderLine"/> constructs one.</summary>
    internal OrderLineModifier(Guid orderLineId, Guid modifierId, string name, Money priceDelta)
    {
        OrderLineId = orderLineId;
        ModifierId = modifierId;
        Name = name;
        PriceDelta = priceDelta;
    }

    /// <summary>The line this selection belongs to.</summary>
    public Guid OrderLineId { get; private set; }

    /// <summary>The catalog modifier this was selected from. A value reference only.</summary>
    public Guid ModifierId { get; private set; }

    /// <summary>Modifier name at the time of sale.</summary>
    public string Name { get; private set; }

    /// <summary>Price delta at the time of sale, per unit of the parent line.</summary>
    public Money PriceDelta { get; private set; }
}
