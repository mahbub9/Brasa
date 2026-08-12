using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;

namespace Brasa.Modules.Catalog.Domain;

/// <summary>
/// One item's overridden price within a <see cref="PriceList"/> (CAT-05).
/// </summary>
/// <remarks>
/// <see cref="MenuItemId"/> is a plain reference, not a foreign key with a
/// navigation — the same "snapshot-style reference, not a live join"
/// pattern <c>OrderLine.MenuItemId</c> uses, deliberately, so a price list
/// entry survives the referenced item being soft-deleted (CAT-18) rather
/// than cascading away silently. Existence is checked once, at creation, by
/// the API layer.
/// </remarks>
public sealed class PriceListEntry : Entity
{
    private PriceListEntry()
    {
        // EF Core materialisation.
    }

    /// <summary>Creates a price override. Use <see cref="PriceList.AddEntry"/>, which also guards against a duplicate item.</summary>
    public PriceListEntry(Guid priceListId, Guid menuItemId, Money price)
    {
        PriceListId = priceListId;
        MenuItemId = menuItemId;
        Price = price;
    }

    /// <summary>The price list this entry belongs to.</summary>
    public Guid PriceListId { get; private set; }

    /// <summary>The menu item this override applies to.</summary>
    public Guid MenuItemId { get; private set; }

    /// <summary>The overridden price at this list's site.</summary>
    public Money Price { get; private set; }
}
