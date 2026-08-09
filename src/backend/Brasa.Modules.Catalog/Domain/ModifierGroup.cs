using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;

namespace Brasa.Modules.Catalog.Domain;

/// <summary>
/// A set of related choices for a menu item, e.g. "Tamanho" (size) or
/// "Extras". CAT-03.
/// </summary>
/// <remarks>
/// Belongs to exactly one <see cref="MenuItem"/> for now — real POS systems
/// often let one group ("Tamanho") apply to many items, but that reuse is a
/// genuine data-modelling step up (shared groups, per-item overrides) that
/// nothing in I1 needs yet. Revisit if a second item ever needs to reuse a
/// group verbatim.
/// </remarks>
public sealed class ModifierGroup : Entity
{
    private readonly List<Modifier> _modifiers = [];

    private ModifierGroup()
    {
        // EF Core materialisation.
        Name = string.Empty;
    }

    /// <summary>Creates a new modifier group for an item.</summary>
    public ModifierGroup(Guid menuItemId, string name, bool isRequired, int minSelect, int maxSelect, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Modifier group name must not be empty.", nameof(name));
        }

        if (minSelect < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minSelect), minSelect, "Minimum selections must not be negative.");
        }

        if (maxSelect < 1 || maxSelect < minSelect)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxSelect), maxSelect, "Maximum selections must be at least 1 and at least the minimum.");
        }

        MenuItemId = menuItemId;
        Name = name;
        IsRequired = isRequired;
        MinSelect = minSelect;
        MaxSelect = maxSelect;
        DisplayOrder = displayOrder;
    }

    /// <summary>The item this group belongs to.</summary>
    public Guid MenuItemId { get; private set; }

    /// <summary>Display name, e.g. "Tamanho".</summary>
    public string Name { get; private set; }

    /// <summary>Whether a guest must choose at least <see cref="MinSelect"/> from this group.</summary>
    public bool IsRequired { get; private set; }

    /// <summary>Fewest selections a valid order line may carry from this group.</summary>
    public int MinSelect { get; private set; }

    /// <summary>Most selections a valid order line may carry from this group.</summary>
    public int MaxSelect { get; private set; }

    /// <summary>Sort position among an item's groups. Lower shows first.</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>The choices within this group.</summary>
    public IReadOnlyList<Modifier> Modifiers => _modifiers;

    /// <summary>Adds a choice to this group.</summary>
    public Modifier AddModifier(string name, Money priceDelta, int displayOrder)
    {
        var modifier = new Modifier(Id, name, priceDelta, displayOrder);
        _modifiers.Add(modifier);
        return modifier;
    }
}
