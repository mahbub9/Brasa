using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;

namespace Brasa.Modules.Catalog.Domain;

/// <summary>
/// One choice within a <see cref="ModifierGroup"/>, e.g. "Extra queijo" (+€1.00)
/// or "Sem cebola" (€0.00).
/// </summary>
public sealed class Modifier : Entity
{
    private Modifier()
    {
        // EF Core materialisation.
        Name = string.Empty;
    }

    /// <summary>Creates a modifier. Only <see cref="ModifierGroup.AddModifier"/> constructs one.</summary>
    internal Modifier(Guid modifierGroupId, string name, Money priceDelta, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Modifier name must not be empty.", nameof(name));
        }

        ModifierGroupId = modifierGroupId;
        Name = name;
        PriceDelta = priceDelta;
        DisplayOrder = displayOrder;
    }

    /// <summary>The group this modifier belongs to.</summary>
    public Guid ModifierGroupId { get; private set; }

    /// <summary>Display name, e.g. "Extra queijo".</summary>
    public string Name { get; private set; }

    /// <summary>
    /// Added to (or, if negative, subtracted from) the parent line's unit
    /// price when selected. Zero for a free option like "sem cebola" — every
    /// choice in a group still needs to exist as a <see cref="Modifier"/> even
    /// when it changes nothing about price, so the guest's choice is recorded.
    /// VAT-inclusive, the same as <c>MenuItem.Price</c> — no separate VAT rate
    /// of its own, it rides the parent line's rate.
    /// </summary>
    public Money PriceDelta { get; private set; }

    /// <summary>Sort position within the group. Lower shows first.</summary>
    public int DisplayOrder { get; private set; }
}
