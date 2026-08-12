using Brasa.Shared.Persistence;

namespace Brasa.Modules.Catalog.Domain;

/// <summary>
/// One item within a <see cref="Combo"/> (CAT-10) — always exactly one unit;
/// see that type's own remarks for why quantity and guest choice are both
/// deferred rather than guessed at.
/// </summary>
/// <remarks>
/// <see cref="MenuItemId"/> is a plain reference, not a foreign key with a
/// navigation — the same "snapshot-style reference, not a live join"
/// pattern <c>PriceListEntry.MenuItemId</c> and <c>OrderLine.MenuItemId</c>
/// both already use. Existence is checked once, at creation, by the API
/// layer.
/// </remarks>
public sealed class ComboComponent : Entity
{
    private ComboComponent()
    {
        // EF Core materialisation.
    }

    /// <summary>Creates a component. Use <see cref="Combo.AddComponent"/>, which also guards against a duplicate item.</summary>
    public ComboComponent(Guid comboId, Guid menuItemId)
    {
        ComboId = comboId;
        MenuItemId = menuItemId;
    }

    /// <summary>The combo this component belongs to.</summary>
    public Guid ComboId { get; private set; }

    /// <summary>The menu item this component rings up.</summary>
    public Guid MenuItemId { get; private set; }
}
